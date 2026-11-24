using AccountingLab.Domain.Model;

namespace AccountingLab.Application.Challenges.DP21TemplateMethod;

public sealed record BankStatementItem(string Id, string Reference, DateOnly Date, decimal Amount);
public sealed record ReconciliationLedgerItem(string Id, string Reference, DateOnly Date, decimal Amount);
public sealed record BankReconciliationRequest(string Mode, IReadOnlyList<BankStatementItem> BankItems, IReadOnlyList<ReconciliationLedgerItem> LedgerItems, decimal Tolerance);
public sealed record ReconciliationMatch(string BankItemId, string LedgerItemId, decimal Difference);
public sealed record BankReconciliationResult(IReadOnlyList<ReconciliationMatch> Matches, IReadOnlyList<string> UnmatchedBankItems, IReadOnlyList<string> UnmatchedLedgerItems);
public interface IBankReconciliationChallenge { BankReconciliationResult Reconcile(BankReconciliationRequest request); }
public sealed class BankReconciliationChallenge : IBankReconciliationChallenge
{
    public BankReconciliationResult Reconcile(BankReconciliationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.Tolerance);
        MoneyRules.Validate(request.Tolerance, allowZero: true);
        ReconciliationProcedure procedure = request.Mode?.ToLowerInvariant() switch
        {
            "exact" => new ExactReconciliation(),
            "tolerance" => new ToleranceReconciliation(request.Tolerance),
            _ => throw new ArgumentException("Modalidad de conciliación desconocida.", nameof(request))
        };
        return procedure.Run(request);
    }

    private abstract class ReconciliationProcedure
    {
        internal BankReconciliationResult Run(BankReconciliationRequest request)
        {
            EnsureUniqueIds(request.BankItems.Select(item => item.Id));
            EnsureUniqueIds(request.LedgerItems.Select(item => item.Id));
            foreach (var item in request.BankItems)
                MoneyRules.ValidateSigned(item.Amount);
            foreach (var item in request.LedgerItems)
                MoneyRules.ValidateSigned(item.Amount);

            var candidates = request.LedgerItems.ToLookup(item => (item.Reference, item.Date));
            var used = new HashSet<string>(StringComparer.Ordinal);
            var matches = new List<ReconciliationMatch>();
            var unmatched = new List<string>();

            foreach (var bank in request.BankItems)
            {
                var candidate = candidates[(bank.Reference, bank.Date)]
                    .Where(item => !used.Contains(item.Id))
                    .Select(item => (Item: item, Difference: Math.Abs(bank.Amount - item.Amount)))
                    .Where(item => AllowsDifference(item.Difference))
                    .OrderBy(item => item.Difference)
                    .FirstOrDefault();
                if (candidate.Item is null)
                {
                    unmatched.Add(bank.Id);
                    continue;
                }

                used.Add(candidate.Item.Id);
                matches.Add(new(bank.Id, candidate.Item.Id, candidate.Difference));
            }

            return new(matches.AsReadOnly(), unmatched.AsReadOnly(),
                Array.AsReadOnly(request.LedgerItems.Where(item => !used.Contains(item.Id)).Select(item => item.Id).ToArray()));
        }

        private static void EnsureUniqueIds(IEnumerable<string> ids)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ids)
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                    throw new ArgumentException("Cada origen debe aportar identificadores únicos y no vacíos.", nameof(ids));
        }

        protected abstract bool AllowsDifference(decimal difference);
    }

    private sealed class ExactReconciliation : ReconciliationProcedure
    {
        protected override bool AllowsDifference(decimal difference) => difference == 0m;
    }

    private sealed class ToleranceReconciliation(decimal tolerance) : ReconciliationProcedure
    {
        protected override bool AllowsDifference(decimal difference) => difference <= tolerance;
    }
}
