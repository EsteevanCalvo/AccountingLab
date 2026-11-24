using System.Collections.ObjectModel;

namespace AccountingLab.Application.Challenges.DP09Decorator;

public sealed record DocumentExportRequest(string Body, IReadOnlyList<string> Features, IReadOnlyDictionary<string, string> Metadata);
public sealed record ExportedAccountingDocument(string Content, IReadOnlyList<string> AppliedFeatures, IReadOnlyDictionary<string, string> Metadata);
public interface IDocumentEnrichmentChallenge { ExportedAccountingDocument Export(DocumentExportRequest request); }
public sealed class DocumentEnrichmentChallenge : IDocumentEnrichmentChallenge
{
    public ExportedAccountingDocument Export(DocumentExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        IDocument document = new PlainDocument(request.Body);
        foreach (var feature in request.Features)
        {
            document = feature?.ToLowerInvariant() switch
            {
                "auditoria" => new AuditDocument(document, feature),
                "metadatos" => new MetadataDocument(document, feature, request.Metadata),
                _ => throw new ArgumentException("Característica de exportación desconocida.", nameof(request))
            };
        }
        return document.Export();
    }

    private interface IDocument
    {
        ExportedAccountingDocument Export();
    }

    private sealed class PlainDocument(string body) : IDocument
    {
        public ExportedAccountingDocument Export() =>
            new(body, Array.Empty<string>(), new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()));
    }

    private abstract class DocumentDecorator(IDocument inner, string feature) : IDocument
    {
        public ExportedAccountingDocument Export()
        {
            var document = inner.Export();
            return Apply(document with { AppliedFeatures = Array.AsReadOnly([.. document.AppliedFeatures, feature]) });
        }

        protected abstract ExportedAccountingDocument Apply(ExportedAccountingDocument document);
    }

    private sealed class AuditDocument(IDocument inner, string feature) : DocumentDecorator(inner, feature)
    {
        protected override ExportedAccountingDocument Apply(ExportedAccountingDocument document) =>
            document with { Content = document.Content + "\n[AUDIT]" };
    }

    private sealed class MetadataDocument(IDocument inner, string feature, IReadOnlyDictionary<string, string> metadata)
        : DocumentDecorator(inner, feature)
    {
        protected override ExportedAccountingDocument Apply(ExportedAccountingDocument document) =>
            document with { Metadata = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(metadata)) };
    }
}
