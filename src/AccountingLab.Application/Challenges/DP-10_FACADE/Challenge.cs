using AccountingLab.Domain.Model;

namespace AccountingLab.Application.Challenges.DP10Facade;

public sealed record ClosingAccountBalance(string AccountCode, decimal Debit, decimal Credit);
public sealed record ClosePeriodRequest(Guid CompanyId, DateOnly From, DateOnly To, IReadOnlyList<ClosingAccountBalance> Balances, int UnpostedDocuments);
public sealed record PeriodClosingSummary(Guid CompanyId, DateOnly From, DateOnly To, bool Closed, decimal TotalDebit, decimal TotalCredit);
public interface IPeriodClosingChallenge { PeriodClosingSummary Close(ClosePeriodRequest request); }
public sealed class PeriodClosingChallenge : IPeriodClosingChallenge
{
    public PeriodClosingSummary Close(ClosePeriodRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePeriod(request);
        EnsureDocumentsPosted(request.UnpostedDocuments);
        var (debit, credit) = ConsolidateBalances(request.Balances);
        return new(request.CompanyId, request.From, request.To, true, debit, credit);
    }

    private static void ValidatePeriod(ClosePeriodRequest request)
    {
        if (request.CompanyId == Guid.Empty || request.From == default || request.To < request.From)
            throw new ArgumentException("Empresa o periodo inválido.", nameof(request));
    }

    private static void EnsureDocumentsPosted(int unpostedDocuments)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(unpostedDocuments);
        if (unpostedDocuments > 0)
            throw new InvalidOperationException("El periodo contiene documentos pendientes.");
    }

    private static (decimal Debit, decimal Credit) ConsolidateBalances(IReadOnlyList<ClosingAccountBalance> balances)
    {
        decimal debit = 0, credit = 0;
        foreach (var balance in balances)
        {
            MoneyRules.Validate(balance.Debit, allowZero: true);
            MoneyRules.Validate(balance.Credit, allowZero: true);
            debit += balance.Debit;
            credit += balance.Credit;
        }
        if (debit != credit)
            throw new InvalidOperationException("El periodo presenta un descuadre contable.");
        return (debit, credit);
    }
}
