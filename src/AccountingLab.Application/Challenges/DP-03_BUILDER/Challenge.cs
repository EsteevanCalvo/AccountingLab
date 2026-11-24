namespace AccountingLab.Application.Challenges.DP03Builder;

public sealed record FinancialReportRequest(string Title, DateOnly From, DateOnly To, IReadOnlyList<string> Sections, IReadOnlyList<string> Annexes);
public sealed record FinancialReport(string Title, DateOnly From, DateOnly To, IReadOnlyList<string> Sections, IReadOnlyList<string> Annexes);
public interface IReportAssemblyChallenge { FinancialReport Build(FinancialReportRequest request); }
public sealed class ReportAssemblyChallenge : IReportAssemblyChallenge
{
    public FinancialReport Build(FinancialReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var builder = new FinancialReportBuilder(request.Title);
        builder.SetPeriod(request.From, request.To);
        foreach (var section in request.Sections)
            builder.AddSection(section);
        foreach (var annex in request.Annexes)
            builder.AddAnnex(annex);
        return builder.Build();
    }

    private sealed class FinancialReportBuilder(string title)
    {
        private readonly List<string> sections = [];
        private readonly List<string> annexes = [];
        private (DateOnly From, DateOnly To)? period;

        internal void SetPeriod(DateOnly from, DateOnly to)
        {
            if (to < from)
                throw new ArgumentException("El fin del informe debe ser igual o posterior al inicio.", nameof(to));
            period = (from, to);
        }

        internal void AddSection(string section) => sections.Add(section);
        internal void AddAnnex(string annex) => annexes.Add(annex);

        internal FinancialReport Build()
        {
            var dates = period ?? throw new InvalidOperationException("El periodo del informe es obligatorio.");
            return new(title, dates.From, dates.To, Array.AsReadOnly(sections.ToArray()), Array.AsReadOnly(annexes.ToArray()));
        }
    }
}
