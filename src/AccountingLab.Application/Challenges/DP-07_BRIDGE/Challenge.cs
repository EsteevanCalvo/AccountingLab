using System.Globalization;
using System.Text.Json;
using AccountingLab.Domain.Model;

namespace AccountingLab.Application.Challenges.DP07Bridge;

public sealed record ReportPresentationRequest(string ReportType, string Format, decimal Amount);
public sealed record RenderedFinancialReport(string ReportType, string Format, string ContentType, string Content);
public interface IFinancialRepresentationChallenge { RenderedFinancialReport Render(ReportPresentationRequest request); }
public sealed class FinancialRepresentationChallenge : IFinancialRepresentationChallenge
{
    public RenderedFinancialReport Render(ReportPresentationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        IReportRenderer renderer = request.Format?.ToLowerInvariant() switch
        {
            "json" => new JsonRenderer(),
            "csv" => new CsvRenderer(),
            _ => throw new ArgumentException("Formato de informe desconocido.", nameof(request))
        };
        FinancialReport report = request.ReportType?.ToLowerInvariant() switch
        {
            "balance" => new BalanceReport(renderer),
            "resultados" => new ResultsReport(renderer),
            _ => throw new ArgumentException("Tipo de informe desconocido.", nameof(request))
        };
        return report.Render(request);
    }

    private abstract class FinancialReport(IReportRenderer renderer)
    {
        protected abstract string Type { get; }

        internal RenderedFinancialReport Render(ReportPresentationRequest request)
        {
            MoneyRules.ValidateSigned(request.Amount);
            var amount = request.Amount.ToString("F4", CultureInfo.InvariantCulture);
            return new(request.ReportType, request.Format, renderer.ContentType, renderer.Write(Type, amount));
        }
    }

    private sealed class BalanceReport(IReportRenderer renderer) : FinancialReport(renderer)
    {
        protected override string Type => "balance";
    }

    private sealed class ResultsReport(IReportRenderer renderer) : FinancialReport(renderer)
    {
        protected override string Type => "resultados";
    }

    private interface IReportRenderer
    {
        string ContentType { get; }
        string Write(string reportType, string amount);
    }

    private sealed class JsonRenderer : IReportRenderer
    {
        public string ContentType => "application/json";
        public string Write(string reportType, string amount) => JsonSerializer.Serialize(new { reportType, amount });
    }

    private sealed class CsvRenderer : IReportRenderer
    {
        public string ContentType => "text/csv";
        public string Write(string reportType, string amount) => $"reportType,amount\r\n{reportType},{amount}\r\n";
    }
}
