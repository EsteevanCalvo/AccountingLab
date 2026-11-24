using AccountingLab.Domain.Model;

namespace AccountingLab.Domain.Challenges.DP17Memento;

public sealed class BudgetDraftLine
{
    public string AccountCode { get; set; } = "";
    public decimal Amount { get; set; }
}
public sealed class BudgetDraft
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool Approved { get; set; }
    public List<BudgetDraftLine> Lines { get; set; } = new();
}
public interface IBudgetSnapshot { }
public interface IBudgetHistoryChallenge
{
    IBudgetSnapshot Save(BudgetDraft draft);
    BudgetDraft Restore(BudgetDraft current, IBudgetSnapshot snapshot);
}
public sealed class BudgetHistoryChallenge : IBudgetHistoryChallenge
{
    public IBudgetSnapshot Save(BudgetDraft draft)
    {
        EnsureDraft(draft);
        foreach (var line in draft.Lines)
            MoneyRules.Validate(line.Amount, allowZero: true);
        return new BudgetSnapshot(draft);
    }

    public BudgetDraft Restore(BudgetDraft current, IBudgetSnapshot snapshot)
    {
        EnsureDraft(current);
        if (snapshot is not BudgetSnapshot saved || current.Id != saved.BudgetId)
            throw new ArgumentException("La instantánea pertenece a otro presupuesto.", nameof(snapshot));
        return saved.Restore();
    }

    private static void EnsureDraft(BudgetDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.Approved)
            throw new InvalidOperationException("Solo se pueden versionar presupuestos en borrador.");
    }

    private sealed class BudgetSnapshot(BudgetDraft draft) : IBudgetSnapshot
    {
        private readonly string name = draft.Name;
        private readonly (string AccountCode, decimal Amount)[] lines =
            draft.Lines.Select(line => (line.AccountCode, line.Amount)).ToArray();

        internal Guid BudgetId { get; } = draft.Id;

        internal BudgetDraft Restore() => new()
        {
            Id = BudgetId,
            Name = name,
            Lines = lines.Select(line => new BudgetDraftLine { AccountCode = line.AccountCode, Amount = line.Amount }).ToList()
        };
    }
}
