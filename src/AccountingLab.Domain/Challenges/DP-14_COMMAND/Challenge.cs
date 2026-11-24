using AccountingLab.Domain.Model;

namespace AccountingLab.Domain.Challenges.DP14Command;

public sealed record AccountingOperationRequest(Guid CompanyId, Guid OperationId, string Kind, Guid TargetId, decimal Amount);
public sealed record AccountingOperationReceipt(Guid OperationId, Guid TargetId, string Kind, decimal AppliedAmount, decimal RemainingAmount, Guid LinkedJournalEntryId, bool Replayed);
public sealed record AccountingOperationContext(decimal OutstandingAmount, bool TargetIsPublished, IReadOnlyList<AccountingOperationReceipt> CompletedOperations);
public interface IIdentifiedOperationChallenge { AccountingOperationReceipt Execute(AccountingOperationRequest request, AccountingOperationContext context); }
public sealed class IdentifiedOperationChallenge : IIdentifiedOperationChallenge
{
    private static readonly Dictionary<string, AccountingCommand> Commands = new(StringComparer.OrdinalIgnoreCase)
    {
        ["apply-payment"] = new ApplyPaymentCommand(),
        ["apply-receipt"] = new ApplyReceiptCommand(),
        ["reverse-voucher"] = new ReverseVoucherCommand()
    };

    public AccountingOperationReceipt Execute(AccountingOperationRequest request, AccountingOperationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        if (request.OperationId == Guid.Empty || request.TargetId == Guid.Empty || request.CompanyId == Guid.Empty)
            throw new ArgumentException("La empresa, operación y objetivo deben identificarse.", nameof(request));
        MoneyRules.Validate(request.Amount);
        if (!Commands.TryGetValue(request.Kind, out var command))
            throw new ArgumentException("Tipo de operación desconocido.", nameof(request));

        var previous = context.CompletedOperations.SingleOrDefault(item => item.OperationId == request.OperationId);
        if (previous is not null)
        {
            if (previous.TargetId != request.TargetId || previous.AppliedAmount != request.Amount
                || !string.Equals(previous.Kind, request.Kind, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("La identificación corresponde a otra operación.");
            return previous with { Replayed = true };
        }

        return command.Execute(request, context);
    }

    private abstract class AccountingCommand(string kind)
    {
        internal AccountingOperationReceipt Execute(AccountingOperationRequest request, AccountingOperationContext context)
        {
            var remaining = CalculateRemaining(request, context);
            return new(request.OperationId, request.TargetId, kind, request.Amount, remaining, Guid.NewGuid(), false);
        }

        protected abstract decimal CalculateRemaining(AccountingOperationRequest request, AccountingOperationContext context);
    }

    private abstract class ApplySettlementCommand(string kind) : AccountingCommand(kind)
    {
        protected override decimal CalculateRemaining(AccountingOperationRequest request, AccountingOperationContext context)
        {
            MoneyRules.Validate(context.OutstandingAmount, allowZero: true);
            if (request.Amount > context.OutstandingAmount)
                throw new InvalidOperationException("El importe supera el saldo de la obligación.");
            return context.OutstandingAmount - request.Amount;
        }
    }

    private sealed class ApplyPaymentCommand() : ApplySettlementCommand("apply-payment");
    private sealed class ApplyReceiptCommand() : ApplySettlementCommand("apply-receipt");

    private sealed class ReverseVoucherCommand() : AccountingCommand("reverse-voucher")
    {
        protected override decimal CalculateRemaining(AccountingOperationRequest request, AccountingOperationContext context)
        {
            if (!context.TargetIsPublished)
                throw new InvalidOperationException("Solo se pueden revertir comprobantes publicados.");
            if (context.CompletedOperations.Any(item => item.TargetId == request.TargetId && item.Kind == "reverse-voucher"))
                throw new InvalidOperationException("El comprobante ya fue revertido.");
            return 0m;
        }
    }
}
