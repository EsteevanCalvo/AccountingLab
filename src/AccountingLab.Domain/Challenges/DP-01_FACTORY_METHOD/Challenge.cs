using AccountingLab.Domain.Model;

namespace AccountingLab.Domain.Challenges.DP01FactoryMethod;

public sealed record VoucherCreationRequest(Guid CompanyId, string Kind, decimal Amount, string Currency);
public sealed record VoucherDraft(Guid Id, Guid CompanyId, string Kind, decimal Amount, string Currency);
public interface IVoucherCreationChallenge { VoucherDraft Create(VoucherCreationRequest request); }
public sealed class VoucherCreationChallenge : IVoucherCreationChallenge
{
    private static readonly Dictionary<string, Creator> Creators = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ingreso"] = new ReceiptCreator(), ["egreso"] = new PaymentCreator(),
        ["ajuste"] = new AdjustmentCreator(), ["traslado"] = new TransferCreator()
    };

    public VoucherDraft Create(VoucherCreationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Creators.TryGetValue(request.Kind, out var creator))
            throw new ArgumentException("Tipo de comprobante desconocido.", nameof(request));
        return creator.Create(request);
    }

    private abstract class Creator
    {
        internal VoucherDraft Create(VoucherCreationRequest request)
        {
            if (request.CompanyId == Guid.Empty || string.IsNullOrWhiteSpace(request.Currency)
                || request.Amount < 0 || request.Amount > MoneyRules.Maximum || decimal.Round(request.Amount, 4) != request.Amount)
                throw new ArgumentException("Empresa, moneda o importe inválidos.", nameof(request));
            return CreateDraft(Guid.NewGuid(), request);
        }

        protected abstract VoucherDraft CreateDraft(Guid id, VoucherCreationRequest request);
    }

    private sealed class ReceiptCreator : Creator
    {
        protected override VoucherDraft CreateDraft(Guid id, VoucherCreationRequest request) =>
            new(id, request.CompanyId, "ingreso", request.Amount, request.Currency);
    }

    private sealed class PaymentCreator : Creator
    {
        protected override VoucherDraft CreateDraft(Guid id, VoucherCreationRequest request) =>
            new(id, request.CompanyId, "egreso", request.Amount, request.Currency);
    }

    private sealed class AdjustmentCreator : Creator
    {
        protected override VoucherDraft CreateDraft(Guid id, VoucherCreationRequest request) =>
            new(id, request.CompanyId, "ajuste", request.Amount, request.Currency);
    }

    private sealed class TransferCreator : Creator
    {
        protected override VoucherDraft CreateDraft(Guid id, VoucherCreationRequest request) =>
            new(id, request.CompanyId, "traslado", request.Amount, request.Currency);
    }
}
