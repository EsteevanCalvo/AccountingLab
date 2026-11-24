namespace AccountingLab.Application.Challenges.DP12Proxy;

public sealed record SensitiveReportRequest(Guid CompanyId, bool MayRead, string ReportKey);
public sealed record SensitiveFinancialReport(Guid CompanyId, string ReportKey, decimal ConfidentialAmount);
public interface ISensitiveReportChallenge { SensitiveFinancialReport Get(SensitiveReportRequest request, Func<SensitiveFinancialReport> loadReport); }
public sealed class SensitiveReportChallenge : ISensitiveReportChallenge
{
    public SensitiveFinancialReport Get(SensitiveReportRequest request, Func<SensitiveFinancialReport> loadReport)
    {
        ArgumentNullException.ThrowIfNull(request);
        DemandAccess(request);
        return VerifyReport(request, loadReport());
    }

    public async Task<SensitiveFinancialReport> GetAsync(SensitiveReportRequest request, Func<Task<SensitiveFinancialReport>> loadReport)
    {
        ArgumentNullException.ThrowIfNull(request);
        DemandAccess(request);
        return VerifyReport(request, await loadReport());
    }

    private static void DemandAccess(SensitiveReportRequest request)
    {
        if (!request.MayRead)
            throw new UnauthorizedAccessException("No tienes permiso para consultar este informe.");
    }

    private static SensitiveFinancialReport VerifyReport(SensitiveReportRequest request, SensitiveFinancialReport report)
    {
        if (report.CompanyId != request.CompanyId || report.ReportKey != request.ReportKey)
            throw new InvalidOperationException("La fuente devolvió un informe distinto del solicitado.");
        return report;
    }
}
