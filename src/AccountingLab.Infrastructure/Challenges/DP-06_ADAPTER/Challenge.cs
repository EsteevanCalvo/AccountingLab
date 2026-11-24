using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AccountingLab.Domain.Model;

namespace AccountingLab.Infrastructure.Challenges.DP06Adapter;

public sealed record BankImportRequest(string Provider, string Payload);
public sealed record ImportedBankMovement(string ExternalId, DateOnly Date, string Description, decimal Debit, decimal Credit, string Currency);
public interface IBankImportChallenge { IReadOnlyList<ImportedBankMovement> Import(BankImportRequest request); }
public sealed class BankImportChallenge : IBankImportChallenge
{
    private static readonly Dictionary<string, IBankAdapter> Adapters = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BancoA"] = new BankAAdapter(), ["BancoB"] = new BankBAdapter()
    };

    public IReadOnlyList<ImportedBankMovement> Import(BankImportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Adapters.TryGetValue(request.Provider, out var adapter))
            throw new ArgumentException("Proveedor bancario desconocido.", nameof(request));

        try
        {
            using var payload = JsonDocument.Parse(request.Payload);
            return payload.RootElement.EnumerateArray().Select(adapter.Adapt).ToList().AsReadOnly();
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new FormatException("El extracto no corresponde al formato del proveedor.", error);
        }
    }

    private interface IBankAdapter
    {
        ImportedBankMovement Adapt(JsonElement row);
    }

    private sealed class BankAAdapter : IBankAdapter
    {
        public ImportedBankMovement Adapt(JsonElement row)
        {
            var debit = Amount(row, "debit");
            var credit = Amount(row, "credit");
            if (debit < 0 || credit < 0)
                throw new FormatException("El débito y el crédito deben ser no negativos.");
            return new(Text(row, "id"), Date(row, "date"), Text(row, "description"), debit, credit, Text(row, "currency"));
        }
    }

    private sealed class BankBAdapter : IBankAdapter
    {
        public ImportedBankMovement Adapt(JsonElement row)
        {
            var signed = Amount(row, "signedAmount");
            return new(Text(row, "reference"), Date(row, "bookedOn"), Text(row, "concept"),
                Math.Max(-signed, 0m), Math.Max(signed, 0m), Text(row, "isoCurrency"));
        }
    }

    private static string Text(JsonElement row, string field) =>
        row.GetProperty(field).GetString() ?? throw new FormatException($"Falta el campo {field}.");

    private static DateOnly Date(JsonElement row, string field) =>
        DateOnly.ParseExact(Text(row, field), "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static readonly Regex DecimalText = new(@"\A-?[0-9]+(?:\.[0-9]{1,4})?\z", RegexOptions.NonBacktracking);

    private static decimal Amount(JsonElement row, string field)
    {
        var text = Text(row, field);
        if (!DecimalText.IsMatch(text)
            || !decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            || Math.Abs(value) > MoneyRules.Maximum)
            throw new FormatException($"Importe bancario inválido en {field}.");
        return value;
    }
}
