using AccountingLab.Domain.Model;

namespace AccountingLab.Application.Challenges.DP15Iterator;

public sealed record LedgerRecord(string EntryCode, DateOnly Date, decimal Debit, decimal Credit);
public sealed record LedgerPage(IReadOnlyList<LedgerRecord> Records);
public interface ILedgerCursor { bool MoveNext(); LedgerRecord Current { get; } }
public interface ILedgerTraversalChallenge { ILedgerCursor Open(IReadOnlyList<LedgerPage> pages); }
public sealed class LedgerTraversalChallenge : ILedgerTraversalChallenge
{
    public ILedgerCursor Open(IReadOnlyList<LedgerPage> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        return new PagedLedgerCursor(pages);
    }

    private sealed class PagedLedgerCursor(IReadOnlyList<LedgerPage> pages) : ILedgerCursor
    {
        private int pageIndex;
        private int recordIndex = -1;
        private LedgerRecord? current;

        public LedgerRecord Current => current
            ?? throw new InvalidOperationException("El cursor no está sobre un registro.");

        public bool MoveNext()
        {
            while (pageIndex < pages.Count)
            {
                var records = pages[pageIndex].Records;
                if (++recordIndex < records.Count)
                {
                    var record = records[recordIndex];
                    MoneyRules.Validate(record.Debit, allowZero: true);
                    MoneyRules.Validate(record.Credit, allowZero: true);
                    current = record;
                    return true;
                }
                pageIndex++;
                recordIndex = -1;
            }
            current = null;
            return false;
        }
    }
}
