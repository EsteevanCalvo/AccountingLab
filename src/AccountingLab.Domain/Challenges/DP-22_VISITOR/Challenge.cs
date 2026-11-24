using System.Collections.ObjectModel;
using AccountingLab.Domain.Model;

namespace AccountingLab.Domain.Challenges.DP22Visitor;

public interface IAccountingAnalysisDocument { Guid Id { get; } }
public sealed record InvoiceAnalysisDocument(Guid Id, decimal NetAmount, decimal TaxAmount) : IAccountingAnalysisDocument;
public sealed record PaymentAnalysisDocument(Guid Id, decimal Amount) : IAccountingAnalysisDocument;
public sealed record AnalysisPostingLine(decimal Debit, decimal Credit);
public sealed record JournalAnalysisDocument(Guid Id, IReadOnlyList<AnalysisPostingLine> Lines) : IAccountingAnalysisDocument;
public sealed record DocumentAnalysisRequest(string View, IReadOnlyList<IAccountingAnalysisDocument> Documents);
public sealed record DocumentAnalysisResult(decimal TotalAmount, int DocumentCount, IReadOnlyDictionary<string, decimal> ByKind);
public interface IDocumentAnalysisChallenge { DocumentAnalysisResult Analyze(DocumentAnalysisRequest request); }
public sealed class DocumentAnalysisChallenge : IDocumentAnalysisChallenge
{
    public DocumentAnalysisResult Analyze(DocumentAnalysisRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        AnalysisVisitor visitor = request.View?.ToLowerInvariant() switch
        {
            "gross" => new GrossVisitor(),
            "count" => new CountVisitor(),
            _ => throw new ArgumentException("Vista de análisis desconocida.", nameof(request))
        };
        foreach (var document in request.Documents)
            AsElement(document).Accept(visitor);
        return visitor.Result();
    }

    private static IVisitableDocument AsElement(IAccountingAnalysisDocument document) => document switch
    {
        InvoiceAnalysisDocument invoice => new InvoiceElement(invoice),
        PaymentAnalysisDocument payment => new PaymentElement(payment),
        JournalAnalysisDocument journal => new JournalElement(journal),
        _ => throw new ArgumentException("Tipo de documento no admitido.", nameof(document))
    };

    private interface IVisitableDocument
    {
        void Accept(AnalysisVisitor visitor);
    }

    private sealed record InvoiceElement(InvoiceAnalysisDocument Document) : IVisitableDocument
    {
        public void Accept(AnalysisVisitor visitor) => visitor.Visit(Document);
    }

    private sealed record PaymentElement(PaymentAnalysisDocument Document) : IVisitableDocument
    {
        public void Accept(AnalysisVisitor visitor) => visitor.Visit(Document);
    }

    private sealed record JournalElement(JournalAnalysisDocument Document) : IVisitableDocument
    {
        public void Accept(AnalysisVisitor visitor) => visitor.Visit(Document);
    }

    private abstract class AnalysisVisitor
    {
        private readonly Dictionary<string, decimal> byKind = new(StringComparer.Ordinal);
        private int count;

        internal abstract void Visit(InvoiceAnalysisDocument invoice);
        internal abstract void Visit(PaymentAnalysisDocument payment);
        internal abstract void Visit(JournalAnalysisDocument journal);

        protected void Add(string kind, decimal amount)
        {
            count++;
            byKind[kind] = byKind.GetValueOrDefault(kind) + amount;
        }

        internal DocumentAnalysisResult Result() =>
            new(byKind.Values.Sum(), count, new ReadOnlyDictionary<string, decimal>(byKind));
    }

    private sealed class GrossVisitor : AnalysisVisitor
    {
        internal override void Visit(InvoiceAnalysisDocument invoice)
        {
            MoneyRules.ValidateSigned(invoice.NetAmount);
            MoneyRules.ValidateSigned(invoice.TaxAmount);
            Add("invoice", invoice.NetAmount + invoice.TaxAmount);
        }

        internal override void Visit(PaymentAnalysisDocument payment)
        {
            MoneyRules.ValidateSigned(payment.Amount);
            Add("payment", payment.Amount);
        }

        internal override void Visit(JournalAnalysisDocument journal)
        {
            decimal debits = 0m, credits = 0m;
            foreach (var line in journal.Lines)
            {
                MoneyRules.Validate(line.Debit, allowZero: true);
                MoneyRules.Validate(line.Credit, allowZero: true);
                debits += line.Debit;
                credits += line.Credit;
            }
            if (debits != credits)
                throw new BusinessRuleException("El asiento analizado debe estar balanceado.");
            Add("journal", debits);
        }
    }

    private sealed class CountVisitor : AnalysisVisitor
    {
        internal override void Visit(InvoiceAnalysisDocument invoice) => Add("invoice", 1m);
        internal override void Visit(PaymentAnalysisDocument payment) => Add("payment", 1m);
        internal override void Visit(JournalAnalysisDocument journal) => Add("journal", 1m);
    }
}
