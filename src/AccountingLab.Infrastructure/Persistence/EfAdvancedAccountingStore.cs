using AccountingLab.Application.Challenges.DP10Facade;
using AccountingLab.Application.Contracts;
using AccountingLab.Domain.Challenges.DP14Command;
using AccountingLab.Domain.Challenges.DP16Mediator;
using AccountingLab.Domain.Model;
using Microsoft.EntityFrameworkCore;

namespace AccountingLab.Infrastructure.Persistence;

internal sealed class EfAdvancedAccountingStore(AccountingDbContext db, TimeProvider clock) : IAdvancedAccountingStore
{
    private readonly IPeriodClosingChallenge closing = new PeriodClosingChallenge();
    private readonly IIdentifiedOperationChallenge commands = new IdentifiedOperationChallenge();
    private readonly InvoiceCoordinationChallenge invoices = new();

    public async Task<PeriodClosingSummary> CloseAsync(ClosePeriodRequest request, Guid actorId, CancellationToken ct)
    {
        if (request.From == default || request.To < request.From)
            throw new BusinessRuleException("El intervalo del periodo contable es inválido.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var company = request.CompanyId;
        await db.LockCompanyAsync(company, ct);
        var period = await db.Periods.SingleOrDefaultAsync(item => item.CompanyId == company
            && item.StartDate == request.From && item.EndDate == request.To, ct)
            ?? throw new ResourceNotFoundException("No se encontró un periodo de la empresa con esas fechas.");
        var balances = await (from entry in db.JournalEntries
                              join line in db.JournalLines on entry.Id equals line.EntryId
                              join account in db.Accounts on line.AccountId equals account.Id
                              where entry.CompanyId == company && entry.Status == "Posted"
                                  && entry.Date >= period.StartDate && entry.Date <= period.EndDate
                              group line by account.Code into rows
                              select new ClosingAccountBalance(rows.Key, rows.Sum(x => x.Debit), rows.Sum(x => x.Credit)))
            .ToListAsync(ct);
        if (period.Status == "Closed")
            return closing.Close(request with { Balances = balances, UnpostedDocuments = 0 });
        if (period.Status != "Open")
            throw new ResourceConflictException("El periodo no está abierto.");

        var unposted = await db.Invoices.CountAsync(invoice => invoice.CompanyId == company
            && invoice.Status == "Draft" && invoice.IssueDate >= period.StartDate && invoice.IssueDate <= period.EndDate, ct);
        unposted += await db.JournalEntries.CountAsync(entry => entry.CompanyId == company && entry.Status != "Posted"
            && entry.Date >= period.StartDate && entry.Date <= period.EndDate, ct);
        PeriodClosingSummary summary;
        try { summary = closing.Close(request with { Balances = balances, UnpostedDocuments = unposted }); }
        catch (InvalidOperationException error) { throw new ResourceConflictException(error.Message); }
        period.Status = "Closed";
        db.AddAudit(company, actorId, period.Id, nameof(AccountingPeriod), "PeriodClosed", clock);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return summary;
    }

    public async Task<AccountingOperationReceipt> ExecuteAsync(AccountingOperationRequest request, Guid actorId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Kind);
        var kind = request.Kind.ToLowerInvariant();
        if (kind is not ("apply-payment" or "apply-receipt" or "reverse-voucher"))
            throw new BusinessRuleException("Tipo de operación desconocido.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var company = request.CompanyId;
        await db.LockCompanyAsync(company, ct);

        var operations = await db.AccountingOperations.AsNoTracking()
            .Where(item => item.CompanyId == company &&
                (item.OperationId == request.OperationId || (kind == "reverse-voucher" && item.TargetId == request.TargetId && item.Kind == kind)))
            .ToListAsync(ct);
        var history = operations.Select(item => item.ToReceipt()).ToArray();
        if (history.Any(item => item.OperationId == request.OperationId))
        {
            var context = new AccountingOperationContext(0m, false, history);
            return RunCommand(request, context);
        }
        if (await db.JournalEntries.AnyAsync(entry => entry.CompanyId == company && entry.OperationId == request.OperationId, ct))
            throw new ResourceConflictException("El identificador ya pertenece a un asiento contable.");

        var now = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(clock.GetUtcNow(), "America/Bogota");
        var postingDate = DateOnly.FromDateTime(now.DateTime);
        if (!await db.Periods.AnyAsync(period => period.CompanyId == company && period.Status == "Open"
            && period.StartDate <= postingDate && postingDate <= period.EndDate, ct))
            throw new ResourceConflictException("La fecha contable requiere un periodo abierto.");

        var (receipt, entry) = kind == "reverse-voucher"
            ? await ReverseAsync(request, history, postingDate, ct)
            : await SettleAsync(request, history, kind == "apply-receipt", postingDate, ct);
        JournalRules.Validate(entry);
        db.JournalEntries.Add(entry);
        db.AccountingOperations.Add(new AccountingOperation
        {
            CompanyId = company, OperationId = receipt.OperationId, TargetId = receipt.TargetId,
            Kind = receipt.Kind, AppliedAmount = receipt.AppliedAmount, RemainingAmount = receipt.RemainingAmount,
            JournalEntryId = receipt.LinkedJournalEntryId
        });
        db.AddAudit(company, actorId, entry.Id, nameof(JournalEntry), receipt.Kind, clock);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return receipt;
    }

    public async Task<InvoiceProcessingResult> ProcessInvoiceAsync(Guid companyId, Guid actorId,
        InvoiceProcessingRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LockCompanyAsync(companyId, ct);
        var invoice = await db.Invoices.SingleOrDefaultAsync(item => item.CompanyId == companyId && item.Id == request.InvoiceId, ct)
            ?? throw new ResourceNotFoundException("No se encontró una factura de la empresa.");
        if (invoice.Amount != request.Amount)
            throw new ResourceConflictException("El importe solicitado no coincide con el borrador registrado.");
        var reference = InvoiceReference(invoice.Id);
        if (invoice.Status == "Issued" && await db.JournalEntries.AnyAsync(entry => entry.CompanyId == companyId
            && entry.OperationId == invoice.Id && entry.Reference == reference, ct))
            return invoices.Process(request);
        if (invoice.Status != "Draft")
            throw new ResourceConflictException("La factura debe permanecer en borrador antes de procesarla.");
        if (!request.IsValid)
            return invoices.Process(request);
        if (!await db.Periods.AnyAsync(period => period.CompanyId == companyId && period.Status == "Open"
            && period.StartDate <= invoice.IssueDate && invoice.IssueDate <= period.EndDate, ct))
            throw new ResourceConflictException("La emisión de la factura requiere un periodo abierto.");
        if (await db.JournalEntries.AnyAsync(entry => entry.CompanyId == companyId && entry.OperationId == invoice.Id, ct))
            throw new ResourceConflictException("El identificador de la factura ya corresponde a un asiento.");

        const string receivable = "1305", revenue = "4135";
        var accounts = await FindPostingAccountsAsync(companyId, receivable, revenue, ct);
        var effects = new InvoiceEffects(db, invoice, actorId, accounts[receivable], accounts[revenue], clock);
        var result = invoices.Process(request, effects);
        if (result.Published)
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        return result;
    }

    private async Task<(AccountingOperationReceipt Receipt, JournalEntry Entry)> ReverseAsync(AccountingOperationRequest request,
        IReadOnlyList<AccountingOperationReceipt> history, DateOnly date, CancellationToken ct)
    {
        var original = await db.JournalEntries.AsNoTracking().Include(entry => entry.Lines)
            .SingleOrDefaultAsync(entry => entry.CompanyId == request.CompanyId && entry.Id == request.TargetId, ct)
            ?? throw new ResourceNotFoundException("No se encontró el comprobante de la empresa.");
        if (original.Lines.Sum(line => line.Debit) != request.Amount)
            throw new BusinessRuleException("La reversión requiere el importe total del comprobante.");

        var receipt = RunCommand(request, new(0m, original.Status == "Posted", history));
        await ReverseLinkedEffectsAsync(original, ct);
        var entry = CreateEntry(request, receipt, date);
        entry.Lines = original.Lines.Select(line => new JournalLine
        {
            AccountId = line.AccountId, CostCenterId = line.CostCenterId, Debit = line.Credit, Credit = line.Debit
        }).ToList();
        return (receipt, entry);
    }

    private async Task ReverseLinkedEffectsAsync(JournalEntry original, CancellationToken ct)
    {
        var company = original.CompanyId;
        var related = await db.AccountingOperations.AsNoTracking().SingleOrDefaultAsync(item =>
            item.CompanyId == company && item.JournalEntryId == original.Id, ct);
        if (related is not null)
        {
            if (related.Kind == "reverse-voucher")
                throw new ResourceConflictException("La reversión es un hecho confirmado. Registra un nuevo hecho para corregirla.");
            var obligation = await db.Obligations.SingleAsync(item =>
                item.CompanyId == company && item.Id == related.TargetId, ct);
            obligation.Status = "Open";
            return;
        }

        if (original.Reference != InvoiceReference(original.OperationId))
            return;
        var invoice = await db.Invoices.SingleOrDefaultAsync(item =>
            item.CompanyId == company && item.Id == original.OperationId && item.Status == "Issued", ct);
        if (invoice is null)
            return;
        if (await db.EffectiveSettlements().AnyAsync(item => item.CompanyId == company && item.TargetId == invoice.Id, ct))
            throw new ResourceConflictException("Revierte primero los cobros aplicados a la factura.");
        var receivable = await db.Obligations.SingleAsync(item => item.CompanyId == company && item.Id == invoice.Id, ct);
        invoice.Status = "Reversed";
        receivable.Status = "Voided";
    }

    private async Task<(AccountingOperationReceipt Receipt, JournalEntry Entry)> SettleAsync(AccountingOperationRequest request,
        IReadOnlyList<AccountingOperationReceipt> history, bool receives, DateOnly date, CancellationToken ct)
    {
        var obligationKind = receives ? "Receivable" : "Payable";
        var obligation = await db.Obligations.SingleOrDefaultAsync(item =>
            item.CompanyId == request.CompanyId && item.Id == request.TargetId && item.Kind == obligationKind, ct)
            ?? throw new ResourceNotFoundException("No se encontró la obligación de la empresa para esta operación.");
        if (obligation.Status != "Open")
            throw new ResourceConflictException("La obligación no está abierta.");
        var settled = await db.EffectiveSettlements()
            .Where(item => item.CompanyId == request.CompanyId && item.TargetId == obligation.Id && item.Kind == request.Kind.ToLowerInvariant())
            .SumAsync(item => (decimal?)item.AppliedAmount, ct) ?? 0m;
        var outstanding = obligation.Amount - settled;
        var receipt = RunCommand(request, new(outstanding, false, history));

        const string cashCode = "1105";
        var obligationCode = receives ? "1305" : "2205";
        var accounts = await FindPostingAccountsAsync(request.CompanyId, cashCode, obligationCode, ct);
        var entry = CreateEntry(request, receipt, date);
        entry.Lines =
        [
            new() { AccountId = accounts[receives ? cashCode : obligationCode], Debit = request.Amount },
            new() { AccountId = accounts[receives ? obligationCode : cashCode], Credit = request.Amount }
        ];
        if (receipt.RemainingAmount == 0m)
            obligation.Status = "Settled";
        return (receipt, entry);
    }

    private AccountingOperationReceipt RunCommand(AccountingOperationRequest request, AccountingOperationContext context)
    {
        try { return commands.Execute(request, context); }
        catch (InvalidOperationException error) { throw new ResourceConflictException(error.Message); }
    }

    private async Task<Dictionary<string, Guid>> FindPostingAccountsAsync(Guid companyId, string firstCode, string secondCode,
        CancellationToken ct)
    {
        var accounts = await db.Accounts.Where(item => item.CompanyId == companyId && !item.IsGroup && item.IsActive
            && (item.Code == firstCode || item.Code == secondCode)).ToDictionaryAsync(item => item.Code, item => item.Id, ct);
        if (accounts.Count != 2)
            throw new BusinessRuleException($"La empresa requiere cuentas de movimiento activas {firstCode} y {secondCode}.");
        return accounts;
    }

    private static JournalEntry CreateEntry(AccountingOperationRequest request, AccountingOperationReceipt receipt, DateOnly date) => new()
    {
        Id = receipt.LinkedJournalEntryId, CompanyId = request.CompanyId, OperationId = request.OperationId,
        Reference = $"{receipt.Kind}/{receipt.TargetId:D}", Date = date
    };

    private static string InvoiceReference(Guid invoiceId) => $"factura/{invoiceId:D}";

    private sealed class InvoiceEffects(AccountingDbContext db, InvoiceDraft invoice, Guid actorId,
        Guid receivable, Guid revenue, TimeProvider clock) : IInvoiceProcessingEffects
    {
        public void Post(InvoiceProcessingRequest request)
        {
            var entry = new JournalEntry
            {
                CompanyId = invoice.CompanyId, OperationId = invoice.Id, Reference = InvoiceReference(invoice.Id),
                Date = invoice.IssueDate,
                Lines =
                [
                    new() { AccountId = receivable, Debit = request.Amount },
                    new() { AccountId = revenue, Credit = request.Amount }
                ]
            };
            JournalRules.Validate(entry);
            db.JournalEntries.Add(entry);
            db.Obligations.Add(new Obligation
            {
                Id = invoice.Id, CompanyId = invoice.CompanyId, ThirdPartyId = invoice.ThirdPartyId,
                Description = invoice.Number, Amount = request.Amount, DueDate = invoice.DueDate
            });
            db.AddAudit(invoice.CompanyId, actorId, entry.Id, nameof(JournalEntry), "InvoicePosted", clock);
        }

        public void Publish(InvoiceProcessingRequest request)
        {
            invoice.Status = "Issued";
            db.AddAudit(invoice.CompanyId, actorId, request.InvoiceId, nameof(InvoiceDraft), "InvoiceIssued", clock);
        }
    }
}

internal static class AccountingWrites
{
    internal static IQueryable<AccountingOperation> EffectiveSettlements(this AccountingDbContext db) =>
        db.AccountingOperations.Where(operation => operation.Kind != "reverse-voucher"
            && !db.AccountingOperations.Any(reversal => reversal.CompanyId == operation.CompanyId
                && reversal.Kind == "reverse-voucher" && reversal.TargetId == operation.JournalEntryId));

    internal static async Task LockCompanyAsync(this AccountingDbContext db, Guid companyId, CancellationToken ct)
    {
        var companies = await db.Companies.FromSqlInterpolated($"SELECT * FROM Companies WHERE Id = {companyId} FOR UPDATE")
            .AsNoTracking().ToListAsync(ct);
        if (companies.Count == 0)
            throw new ResourceNotFoundException("No se encontró la empresa activa.");
    }

    internal static void AddAudit(this AccountingDbContext db, Guid companyId, Guid actorId, Guid resourceId,
        string resource, string action, TimeProvider clock) =>
        db.Audit.Add(new AuditEvent
        {
            CompanyId = companyId, UserId = actorId, ResourceId = resourceId, Resource = resource,
            Action = action, OccurredAt = clock.GetUtcNow().UtcDateTime
        });
}
