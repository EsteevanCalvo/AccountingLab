using AccountingLab.Domain.Model;

namespace AccountingLab.Domain.Challenges.DP20Strategy;

public sealed record FictionalTaxRule(string Name, string OperationKind, DateOnly ValidFrom, DateOnly ValidTo, string CalculationKind, decimal Rate, decimal FixedAmount);
public sealed record TaxCalculationRequest(decimal BaseAmount, string OperationKind, DateOnly AccountingDate, IReadOnlyList<FictionalTaxRule> Rules);
public sealed record TaxCalculationResult(decimal BaseAmount, decimal AppliedRate, decimal TaxAmount, string RuleName);
public interface IConfigurableTaxChallenge { TaxCalculationResult Calculate(TaxCalculationRequest request); }
public sealed class ConfigurableTaxChallenge : IConfigurableTaxChallenge
{
    private static readonly IReadOnlyDictionary<string, ITaxStrategy> Strategies =
        new Dictionary<string, ITaxStrategy>(StringComparer.OrdinalIgnoreCase)
        {
            ["percentage"] = new PercentageTax(), ["fixed"] = new FixedTax()
        };

    public TaxCalculationResult Calculate(TaxCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        MoneyRules.ValidateSigned(request.BaseAmount);
        var rules = request.Rules.Where(rule =>
            string.Equals(rule.OperationKind, request.OperationKind, StringComparison.OrdinalIgnoreCase)
            && rule.ValidFrom <= request.AccountingDate && request.AccountingDate <= rule.ValidTo).Take(2).ToArray();
        if (rules.Length != 1)
            throw new InvalidOperationException(rules.Length == 0
                ? "No existe una regla tributaria ficticia vigente para la operación."
                : "Hay reglas tributarias ficticias vigentes superpuestas para la operación.");

        var rule = rules[0];
        if (!Strategies.TryGetValue(rule.CalculationKind, out var strategy))
            throw new ArgumentException("Método de cálculo tributario desconocido.", nameof(request));

        var (rate, value) = strategy.Calculate(request.BaseAmount, rule);
        return new(request.BaseAmount, rate, decimal.Round(value, 4, MidpointRounding.AwayFromZero), rule.Name);
    }

    private interface ITaxStrategy
    {
        (decimal Rate, decimal Amount) Calculate(decimal taxBase, FictionalTaxRule rule);
    }

    private sealed class PercentageTax : ITaxStrategy
    {
        public (decimal Rate, decimal Amount) Calculate(decimal taxBase, FictionalTaxRule rule) =>
            rule.Rate is >= 0m and <= 1m && ((decimal.GetBits(rule.Rate)[3] >> 16) & 0xff) <= 6
                ? (rule.Rate, taxBase * rule.Rate)
                : throw new BusinessRuleException("La tasa ficticia requiere un valor entre cero y uno y hasta seis decimales.");
    }

    private sealed class FixedTax : ITaxStrategy
    {
        public (decimal Rate, decimal Amount) Calculate(decimal taxBase, FictionalTaxRule rule)
        {
            MoneyRules.ValidateSigned(rule.FixedAmount);
            return (0m, rule.FixedAmount);
        }
    }
}
