using AccountingLab.Api.Transport;
using AccountingLab.Application.Contracts;
using AccountingLab.Domain.Education;
using AccountingLab.Domain.Model;
using AccountingLab.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using D01 = AccountingLab.Domain.Challenges.DP01FactoryMethod;
using D02 = AccountingLab.Domain.Challenges.DP02AbstractFactory;
using D03 = AccountingLab.Application.Challenges.DP03Builder;
using D04 = AccountingLab.Domain.Challenges.DP04Prototype;
using D05 = AccountingLab.Domain.Challenges.DP05Singleton;
using D06 = AccountingLab.Infrastructure.Challenges.DP06Adapter;
using D07 = AccountingLab.Application.Challenges.DP07Bridge;
using D08 = AccountingLab.Domain.Challenges.DP08Composite;
using D09 = AccountingLab.Application.Challenges.DP09Decorator;
using D10 = AccountingLab.Application.Challenges.DP10Facade;
using D11 = AccountingLab.Domain.Challenges.DP11Flyweight;
using D12 = AccountingLab.Application.Challenges.DP12Proxy;
using D13 = AccountingLab.Domain.Challenges.DP13ChainOfResponsibility;
using D14 = AccountingLab.Domain.Challenges.DP14Command;
using D15 = AccountingLab.Application.Challenges.DP15Iterator;
using D16 = AccountingLab.Domain.Challenges.DP16Mediator;
using D17 = AccountingLab.Domain.Challenges.DP17Memento;
using D18 = AccountingLab.Domain.Challenges.DP18Observer;
using D19 = AccountingLab.Domain.Challenges.DP19State;
using D20 = AccountingLab.Domain.Challenges.DP20Strategy;
using D21 = AccountingLab.Application.Challenges.DP21TemplateMethod;
using D22 = AccountingLab.Domain.Challenges.DP22Visitor;

namespace AccountingLab.Api.Controllers;

public sealed record IdentifiedOperationInput(D14.AccountingOperationRequest Request, D14.AccountingOperationContext Context);

[ApiController, Route("api"), Authorize, ServiceFilter(typeof(TenantAccessFilter))]
[ChallengeInputFilter]
[ProducesResponseType(typeof(ProblemDetails), 400)]
[ProducesResponseType(typeof(ProblemDetails), 401)]
[ProducesResponseType(typeof(ProblemDetails), 403)]
public sealed class ChallengesController(TenantContext tenant, CatalogService service, IAdvancedAccountingStore accounting) : ControllerBase
{
    [HttpGet("challenges")]
    public IActionResult Catalog() => Ok(ChallengeCatalog.Items.Select(x => new { x.Id, x.Name, x.Family, x.Context, x.ReferenceUrl, Available = true, Verification = "NotExecuted" }));

    [HttpPost("vouchers/generate")]
    public IActionResult Voucher(D01.VoucherCreationRequest input) => Ok(new D01.VoucherCreationChallenge().Create(input with { CompanyId = tenant.CompanyId }));
    [HttpPost("companies/profile-services")]
    public IActionResult Profile(D02.AccountingProfileRequest input) => Ok(new D02.AccountingProfileChallenge().CreateFamily(input));
    [HttpPost("reports/build")]
    public IActionResult Report(D03.FinancialReportRequest input) => Ok(new D03.ReportAssemblyChallenge().Build(input));
    [HttpGet("reports/balance-sheet")]
    public IActionResult BalanceSheet(DateOnly? from, DateOnly? to) => Report(new("Balance general", from ?? new DateOnly(2026, 1, 1), to ?? new DateOnly(2026, 12, 31), ["Activo", "Pasivo", "Patrimonio"], []));
    [HttpPost("templates/copy")]
    public IActionResult CopyTemplate(D04.RecurringTemplate input) => Ok(new D04.TemplateCopyChallenge().Copy(input));
    [HttpPost("configuration/registry")]
    public IActionResult Registry() => Ok(new D05.GlobalRegistryChallenge().Acquire());
    [HttpPost("bank-statements/import")]
    public IActionResult Import(D06.BankImportRequest input) => Ok(new D06.BankImportChallenge().Import(input));
    [HttpPost("reports/render")]
    public IActionResult Render(D07.ReportPresentationRequest input) => Ok(new D07.FinancialRepresentationChallenge().Render(input));
    [HttpPost("accounts/tree-total")]
    public IActionResult Tree(D08.AccountHierarchy input) => Ok(new D08.AccountTreeChallenge().Total(input));
    [HttpPost("exports/enriched")]
    public IActionResult Enrich(D09.DocumentExportRequest input) => Ok(new D09.DocumentEnrichmentChallenge().Export(input));
    [HttpPost("accounting-periods/close")]
    public async Task<IActionResult> Close(D10.ClosePeriodRequest input, CancellationToken ct) =>
        Ok(await accounting.CloseAsync(input with { CompanyId = tenant.CompanyId }, tenant.UserId, ct));
    [HttpPost("movements/classifications")]
    public IActionResult Classifications(IReadOnlyList<D11.MovementClassificationInput> input) => Ok(new D11.SharedClassificationChallenge().Prepare(input));
    [HttpPost("reports/confidential")]
    public async Task<IActionResult> Confidential(D12.SensitiveReportRequest input, CancellationToken ct = default)
    {
        try
        {
            var authorized = input with { CompanyId = tenant.CompanyId, MayRead = tenant.Role == "Administrador" };
            return Ok(await new D12.SensitiveReportChallenge().GetAsync(authorized, async () =>
            {
                var summary = await service.SummaryAsync(tenant.CompanyId, ct);
                return new D12.SensitiveFinancialReport(tenant.CompanyId, input.ReportKey, summary.Debits);
            }));
        }
        catch (UnauthorizedAccessException error)
        {
            throw new AccessDeniedException(error.Message);
        }
    }
    [HttpPost("payments/approve")]
    public IActionResult Approve(D13.PaymentApprovalRequest input) => Ok(new D13.PaymentApprovalChallenge().Evaluate(input));
    [HttpPost("operations/execute")]
    public async Task<IActionResult> ExecuteOperation(IdentifiedOperationInput input, CancellationToken ct) =>
        Ok(await accounting.ExecuteAsync(input.Request with { CompanyId = tenant.CompanyId }, tenant.UserId, ct));
    [HttpPost("ledger/traverse")]
    public IActionResult Traverse(IReadOnlyList<D15.LedgerPage> input)
    {
        var cursor = new D15.LedgerTraversalChallenge().Open(input);
        var records = new List<D15.LedgerRecord>();
        while (cursor.MoveNext())
            records.Add(cursor.Current);
        return Ok(records);
    }
    [HttpPost("invoices/process")]
    public async Task<IActionResult> Process(D16.InvoiceProcessingRequest input, CancellationToken ct) =>
        Ok(await accounting.ProcessInvoiceAsync(tenant.CompanyId, tenant.UserId, input, ct));
    [HttpPost("budgets/snapshots")]
    public async Task<IActionResult> Snapshot(D17.BudgetDraft input, [FromServices] ChallengeSessions sessions, CancellationToken ct)
    {
        await EnsureBudgetDraftAsync(input.Id, ct);
        return Ok(new { SnapshotId = sessions.SaveBudget(tenant.CompanyId, tenant.UserId, input) });
    }
    [HttpPost("budgets/snapshots/{snapshotId:guid}/restore")]
    public async Task<IActionResult> RestoreSnapshot(Guid snapshotId, D17.BudgetDraft input, [FromServices] ChallengeSessions sessions, CancellationToken ct)
    {
        await EnsureBudgetDraftAsync(input.Id, ct);
        return Ok(sessions.RestoreBudget(tenant.CompanyId, tenant.UserId, snapshotId, input));
    }
    [HttpPost("notifications/publish")]
    public IActionResult Publish(D18.FinancialEvent input, [FromServices] ChallengeSessions sessions) =>
        Ok(sessions.Publish(tenant.CompanyId, input));
    [HttpPost("notifications/subscriptions")]
    public IActionResult Subscribe([FromServices] ChallengeSessions sessions)
    {
        sessions.Subscribe(tenant.CompanyId, tenant.UserId);
        return NoContent();
    }
    [HttpDelete("notifications/subscriptions")]
    public IActionResult Unsubscribe([FromServices] ChallengeSessions sessions)
    {
        sessions.Unsubscribe(tenant.CompanyId, tenant.UserId);
        return NoContent();
    }
    [HttpGet("notifications/inbox")]
    public IActionResult Inbox([FromServices] ChallengeSessions sessions) =>
        Ok(sessions.ReadInbox(tenant.CompanyId, tenant.UserId));
    [HttpPost("invoices/transition")]
    public IActionResult Transition(D19.InvoiceLifecycleRequest input) => Evaluate(() => new D19.InvoiceLifecycleChallenge().Transition(input));
    [HttpPost("taxes/calculate")]
    public IActionResult Taxes(D20.TaxCalculationRequest input) => Evaluate(() => new D20.ConfigurableTaxChallenge().Calculate(input));
    [HttpPost("bank-reconciliation/reconcile")]
    public IActionResult Reconcile(D21.BankReconciliationRequest input) => Ok(new D21.BankReconciliationChallenge().Reconcile(input));
    [HttpPost("documents/analyze")]
    public IActionResult Analyze(D22.DocumentAnalysisRequest input) => Ok(new D22.DocumentAnalysisChallenge().Analyze(input));

    // Los ejemplos calculan resultados con los patrones sin confirmar operaciones de la empresa.
    [HttpPost("challenges/{id}/execute")]
    public async Task<IActionResult> Demonstrate(string id)
    {
        var companyId = tenant.CompanyId;
        return id.ToUpperInvariant() switch
        {
            "DP-01" => Voucher(new(companyId, "Ingreso", 100m, "COP")),
            "DP-02" => Profile(new("comercial", "DEMO-A")),
            "DP-03" => Report(new("Informe educativo", new(2026, 1, 1), new(2026, 12, 31), ["Balance"], [])),
            "DP-04" => CopyTemplate(new D04.RecurringTemplate
                { Id = Guid.NewGuid(), Name = "Plantilla de arriendo", Lines = [new() { AccountCode = "5105", Amount = 450m, Tags = ["mensual"] }] }),
            "DP-05" => Registry(),
            "DP-06" => Import(new("BancoB", """[{"reference":"DEMO-1","bookedOn":"2026-01-12","concept":"Pago","signedAmount":"-10.1250","isoCurrency":"COP"}]""")),
            "DP-07" => Render(new("balance", "csv", 100m)),
            "DP-08" => Tree(new("11", false, 0m, [new("1105", true, 100m, []), new("1110", true, 25m, [])])),
            "DP-09" => Enrich(new("Documento educativo", ["auditoria", "metadatos"], new Dictionary<string, string> { ["origen"] = "Ejemplo académico" })),
            "DP-10" => Ok(new D10.PeriodClosingChallenge().Close(new(companyId, new(2026, 1, 1), new(2026, 12, 31),
                [new("1105", 100m, 0m), new("3105", 0m, 100m)], 0))),
            "DP-11" => Classifications([new(Guid.NewGuid(), 100m, "VENTA", "Venta", "DEMO-A"), new(Guid.NewGuid(), 25m, "VENTA", "Venta", "DEMO-A")]),
            "DP-12" => await Confidential(new(companyId, true, "Balance")),
            "DP-13" => Approve(new(100m, 500m, true, 200m, ["fondos", "politica", "limite"])),
            "DP-14" => DemonstrateCommand(companyId),
            "DP-15" => Traverse([new([new("DEMO-1", new(2026, 1, 1), 25m, 0m)]), new([]), new([new("DEMO-2", new(2026, 1, 2), 0m, 25m)])]),
            "DP-16" => Ok(new D16.InvoiceCoordinationChallenge().Process(
                new(Guid.NewGuid(), 100m, true, ["validacion", "contabilizacion", "publicacion"]))),
            "DP-17" => DemonstrateSnapshot(),
            "DP-18" => DemonstrateNotifications(),
            "DP-19" => Transition(new("draft", "emit", 100m, new(2026, 12, 31), new(2026, 10, 23))),
            "DP-20" => Taxes(new(100m, "venta", new(2026, 10, 23),
                [new("DEMO-A", "venta", new(2026, 1, 1), new(2026, 12, 31), "percentage", 0.1375m, 0m)])),
            "DP-21" => Reconcile(new("tolerance", [new("B-1", "R-1", new(2026, 1, 1), 25.0001m)],
                [new("L-1", "R-1", new(2026, 1, 1), 25m)], 0.0001m)),
            "DP-22" => Analyze(new("gross", [new D22.InvoiceAnalysisDocument(Guid.NewGuid(), 100m, 13m),
                new D22.PaymentAnalysisDocument(Guid.NewGuid(), 25m)])),
            _ => throw new ResourceNotFoundException("No existe ese desafío académico.")
        };
    }

    private IActionResult DemonstrateCommand(Guid companyId)
    {
        var command = new D14.IdentifiedOperationChallenge();
        var request = new D14.AccountingOperationRequest(companyId, Guid.NewGuid(), "apply-receipt", Guid.NewGuid(), 100m);
        var first = command.Execute(request, new(200m, false, []));
        return Ok(new { First = first, Replay = command.Execute(request, new(first.RemainingAmount, false, [first])) });
    }

    private IActionResult DemonstrateSnapshot()
    {
        var history = new D17.BudgetHistoryChallenge();
        var draft = new D17.BudgetDraft { Id = Guid.NewGuid(), Name = "Borrador educativo",
            Lines = [new() { AccountCode = "5105", Amount = 100m }] };
        var snapshot = history.Save(draft);
        draft.Lines[0].Amount = 125m;
        return Ok(new { BeforeRestore = draft, Restored = history.Restore(draft, snapshot) });
    }

    private IActionResult DemonstrateNotifications()
    {
        var publisher = new D18.FinancialNotificationsChallenge();
        var deliveries = new Dictionary<string, D18.FinancialEvent>();
        publisher.Subscribe("tesoreria", item => deliveries.Add("tesoreria", item));
        publisher.Subscribe("auditoria", item => deliveries.Add("auditoria", item));
        publisher.Unsubscribe("auditoria");
        var recipients = publisher.Publish(new(Guid.NewGuid(), "pago-registrado", 100m));
        return Ok(new { Recipients = recipients, Deliveries = deliveries });
    }

    private IActionResult Evaluate<T>(Func<T> evaluate)
    {
        try { return Ok(evaluate()); }
        catch (InvalidOperationException error) { throw new BusinessRuleException(error.Message); }
    }

    private async Task EnsureBudgetDraftAsync(Guid id, CancellationToken ct)
    {
        var stored = await service.GetAsync<BudgetDraft>(tenant.CompanyId, id, ct);
        if (stored.Status != "Draft")
            throw new ResourceConflictException("La versión requiere un presupuesto registrado en borrador.");
    }
}
