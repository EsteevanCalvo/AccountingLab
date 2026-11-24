using AccountingLab.Domain.Challenges.DP14Command;
using AccountingLab.Domain.Model;

namespace AccountingLab.Infrastructure.Persistence;

internal sealed class AccountingOperation : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Guid OperationId { get; set; }
    public Guid TargetId { get; set; }
    public string Kind { get; set; } = "";
    public decimal AppliedAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public Guid JournalEntryId { get; set; }

    internal AccountingOperationReceipt ToReceipt() =>
        new(OperationId, TargetId, Kind, AppliedAmount, RemainingAmount, JournalEntryId, false);
}
