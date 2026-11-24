using AccountingLab.Application.Challenges.DP10Facade;
using AccountingLab.Domain.Challenges.DP14Command;
using AccountingLab.Domain.Challenges.DP16Mediator;

namespace AccountingLab.Application.Contracts;

public interface IAdvancedAccountingStore
{
    Task<PeriodClosingSummary> CloseAsync(ClosePeriodRequest request, Guid actorId, CancellationToken ct);
    Task<AccountingOperationReceipt> ExecuteAsync(AccountingOperationRequest request, Guid actorId, CancellationToken ct);
    Task<InvoiceProcessingResult> ProcessInvoiceAsync(Guid companyId, Guid actorId, InvoiceProcessingRequest request, CancellationToken ct);
}
