using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AccountingLab.IntegrationTests;

[Collection(MySqlApiCollection.Name)]
public sealed class ImplementedChallengesTests(MySqlApiFixture fixture)
{
    [Fact]
    public async Task EducationalRoutesReturnRealResultsWithoutPostingTransactions()
    {
        using var workspace = await fixture.CreateWorkspaceAsync();
        var catalog = await workspace.GetAsync("/api/challenges");
        Assert.Equal(22, catalog.GetArrayLength());
        var auditCount = await fixture.WithDatabaseAsync(db => db.Audit.CountAsync(item => item.CompanyId == workspace.CompanyId));

        foreach (var challenge in catalog.EnumerateArray())
        {
            Assert.True(challenge.GetProperty("available").GetBoolean());
            var id = challenge.GetProperty("id").GetString();
            var result = await workspace.PostAsync($"/api/challenges/{id}/execute", new { });
            Assert.NotEqual(JsonValueKind.Null, result.ValueKind);
            switch (id)
            {
                case "DP-08":
                    Assert.Equal(125m, Amount(result));
                    break;
                case "DP-14":
                    Assert.Equal(result.GetProperty("first").GetProperty("linkedJournalEntryId").GetGuid(),
                        result.GetProperty("replay").GetProperty("linkedJournalEntryId").GetGuid());
                    break;
                case "DP-15":
                    Assert.Equal(2, result.GetArrayLength());
                    break;
                case "DP-18":
                    Assert.Equal("tesoreria", Assert.Single(result.GetProperty("recipients").EnumerateArray()).GetString());
                    break;
                case "DP-22":
                    Assert.Equal(138m, Amount(result.GetProperty("totalAmount")));
                    break;
            }
        }

        Assert.Equal(0, await fixture.WithDatabaseAsync(db => db.JournalEntries.CountAsync(item => item.CompanyId == workspace.CompanyId)));
        Assert.Equal(auditCount, await fixture.WithDatabaseAsync(db => db.Audit.CountAsync(item => item.CompanyId == workspace.CompanyId)));
    }

    [Fact]
    public async Task InvoicePublicationUnblocksClosingAndProtectsPostedHistory()
    {
        using var workspace = await fixture.CreateWorkspaceAsync();
        var (cash, capital) = await workspace.AddJournalDataAsync();
        await AddPostingAccounts(workspace, "1305", "4135");
        var party = await workspace.AddPartyAsync();
        var invoice = await workspace.PostAsync("/api/invoices", new
        {
            thirdPartyId = party, number = "IT-001", issueDate = "2026-10-23", dueDate = "2026-12-01", amount = "75.1250"
        });
        var id = invoice.GetProperty("id").GetGuid();
        var closing = new
        {
            companyId = Guid.NewGuid(), from = "2026-01-01", to = "2026-12-31", balances = Array.Empty<object>(), unpostedDocuments = 0
        };
        using var rejected = await workspace.Client.PostAsJsonAsync("/api/accounting-periods/close", closing);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);

        var processing = new
        {
            invoiceId = id, amount = "75.1250", isValid = true,
            participants = new[] { "publicacion", "validacion", "contabilizacion" }
        };
        var result = await workspace.PostAsync("/api/invoices/process", processing);
        Assert.True(result.GetProperty("published").GetBoolean());
        Assert.True((await workspace.PostAsync("/api/invoices/process", processing)).GetProperty("published").GetBoolean());
        var receipts = await workspace.GetAsync("/api/receivables");
        Assert.Equal(id, Assert.Single(receipts.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());

        var closed = await workspace.PostAsync("/api/accounting-periods/close", closing);
        Assert.True(closed.GetProperty("closed").GetBoolean());
        Assert.Equal(workspace.CompanyId, closed.GetProperty("companyId").GetGuid());
        Assert.Equal(75.1250m, Amount(closed.GetProperty("totalDebit")));
        Assert.Equal(75.1250m, Amount(closed.GetProperty("totalCredit")));
        Assert.True((await workspace.PostAsync("/api/accounting-periods/close", closing)).GetProperty("closed").GetBoolean());
        using var outside = await workspace.Client.PostAsJsonAsync("/api/journal-entries", TestWorkspace.Journal(Guid.NewGuid(), cash, capital, "Cierre"));
        Assert.Equal(HttpStatusCode.BadRequest, outside.StatusCode);
        Assert.Equal(1, await fixture.WithDatabaseAsync(db => db.Audit.CountAsync(item => item.CompanyId == workspace.CompanyId && item.Action == "PeriodClosed")));
    }

    [Fact]
    public async Task CommandsAreAtomicTenantScopedIdempotentAndReverseLinkedBalances()
    {
        using var workspace = await fixture.CreateWorkspaceAsync();
        using var another = await fixture.CreateWorkspaceAsync();
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Bogota").DateTime);
        foreach (var owner in new[] { workspace, another })
            await owner.PostAsync("/api/periods", new { name = "Periodo", startDate = date.AddDays(-1), endDate = date.AddDays(1) });
        await AddPostingAccounts(workspace, "1105", "1305", "2205");
        var thirdPartyId = await workspace.AddPartyAsync();
        var receivable = await workspace.PostAsync("/api/receivables", new { thirdPartyId, description = "Venta", amount = "100.0000", dueDate = date });
        var payable = await workspace.PostAsync("/api/payables", new { thirdPartyId, description = "Compra", amount = "75.0000", dueDate = date });
        var receivableId = receivable.GetProperty("id").GetGuid();
        var payableId = payable.GetProperty("id").GetGuid();

        var receiptRequest = Operation(workspace.CompanyId, Guid.NewGuid(), "apply-receipt", receivableId, "25.1250");
        using var forbiddenTarget = await another.Client.PostAsJsonAsync("/api/operations/execute", receiptRequest);
        Assert.Equal(HttpStatusCode.NotFound, forbiddenTarget.StatusCode);
        var executions = await Task.WhenAll(
            workspace.PostAsync("/api/operations/execute", receiptRequest),
            workspace.PostAsync("/api/operations/execute", receiptRequest));
        Assert.Single(executions, item => item.GetProperty("replayed").GetBoolean());
        var linked = executions[0].GetProperty("linkedJournalEntryId").GetGuid();
        Assert.All(executions, item => Assert.Equal(linked, item.GetProperty("linkedJournalEntryId").GetGuid()));
        Assert.Equal(74.875m, Amount(executions[0].GetProperty("remainingAmount")));

        var failingId = Guid.NewGuid();
        var paymentRequest = Operation(workspace.CompanyId, failingId, "apply-payment", payableId, "75.0000");
        fixture.Factory.Failure.OperationToFail = failingId;
        try
        {
            using var failed = await workspace.Client.PostAsJsonAsync("/api/operations/execute", paymentRequest);
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        }
        finally { fixture.Factory.Failure.OperationToFail = null; }
        var beforePayment = await workspace.GetAsync("/api/reports/summary");
        Assert.Equal(75m, Amount(beforePayment.GetProperty("payableTotal")));
        Assert.Equal(1, beforePayment.GetProperty("entryCount").GetInt32());

        var payment = await workspace.PostAsync("/api/operations/execute", paymentRequest);
        Assert.False(payment.GetProperty("replayed").GetBoolean());
        var paymentEntry = payment.GetProperty("linkedJournalEntryId").GetGuid();
        var reversal = await workspace.PostAsync("/api/operations/execute",
            Operation(workspace.CompanyId, Guid.NewGuid(), "reverse-voucher", paymentEntry, "75.0000"));
        Assert.Equal(paymentEntry, reversal.GetProperty("targetId").GetGuid());

        var after = await workspace.GetAsync("/api/reports/summary");
        Assert.Equal(74.875m, Amount(after.GetProperty("receivableTotal")));
        Assert.Equal(75m, Amount(after.GetProperty("payableTotal")));
        Assert.Equal(3, after.GetProperty("entryCount").GetInt32());
        Assert.Equal(Amount(after.GetProperty("debits")), Amount(after.GetProperty("credits")));
        var entries = await fixture.WithDatabaseAsync(db => db.JournalEntries.Where(item => item.CompanyId == workspace.CompanyId)
            .Include(item => item.Lines).ToListAsync());
        var original = entries.Single(item => item.Id == paymentEntry);
        var opposite = entries.Single(item => item.Id == reversal.GetProperty("linkedJournalEntryId").GetGuid());
        Assert.All(original.Lines, line => Assert.Contains(opposite.Lines, other => line.AccountId == other.AccountId
            && line.Debit == other.Credit && line.Credit == other.Debit));
    }

    [Fact]
    public async Task SnapshotAndObserverSessionsRespectTenantBoundaries()
    {
        using var workspace = await fixture.CreateWorkspaceAsync();
        using var another = await fixture.CreateWorkspaceAsync();
        var budget = await workspace.PostAsync("/api/budgets", new { name = "Inicial", year = 2026, amount = "100.0000" });
        var budgetId = budget.GetProperty("id").GetGuid();
        var snapshot = await workspace.PostAsync("/api/budgets/snapshots", new
        {
            id = budgetId, name = "Inicial", approved = false,
            lines = new[] { new { accountCode = "5105", amount = "100.0000" } }
        });
        var route = $"/api/budgets/snapshots/{snapshot.GetProperty("snapshotId").GetGuid():D}/restore";
        var current = new { id = budgetId, name = "Editado", approved = false, lines = Array.Empty<object>() };
        using var inaccessible = await another.Client.PostAsJsonAsync(route, current);
        Assert.Equal(HttpStatusCode.NotFound, inaccessible.StatusCode);
        var restored = await workspace.PostAsync(route, current);
        Assert.Equal("Inicial", restored.GetProperty("name").GetString());
        Assert.Equal(100m, Amount(Assert.Single(restored.GetProperty("lines").EnumerateArray()).GetProperty("amount")));

        using var subscription = await workspace.Client.PostAsync("/api/notifications/subscriptions", null);
        Assert.Equal(HttpStatusCode.NoContent, subscription.StatusCode);
        var eventId = Guid.NewGuid();
        var payload = new { eventId, kind = "pago-registrado", amount = "5.0000" };
        Assert.Empty((await another.PostAsync("/api/notifications/publish", payload)).EnumerateArray());
        Assert.Single((await workspace.PostAsync("/api/notifications/publish", payload)).EnumerateArray());
        var inbox = await workspace.GetAsync("/api/notifications/inbox");
        Assert.Equal(eventId, Assert.Single(inbox.EnumerateArray()).GetProperty("eventId").GetGuid());
        using var cancellation = await workspace.Client.DeleteAsync("/api/notifications/subscriptions");
        Assert.Equal(HttpStatusCode.NoContent, cancellation.StatusCode);
        Assert.Empty((await workspace.PostAsync("/api/notifications/publish", payload)).EnumerateArray());
    }

    [Fact]
    public async Task InvoiceReversalPreservesOriginalAndRemovesOutstandingReceivable()
    {
        using var workspace = await fixture.CreateWorkspaceAsync();
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "America/Bogota").DateTime);
        await workspace.PostAsync("/api/periods", new { name = "Periodo", startDate = date.AddDays(-1), endDate = date.AddDays(1) });
        await AddPostingAccounts(workspace, "1305", "4135");
        var thirdPartyId = await workspace.AddPartyAsync();
        var invoice = await workspace.PostAsync("/api/invoices", new
        {
            thirdPartyId, number = "IT-REV", issueDate = date, dueDate = date, amount = "50.0001"
        });
        var invoiceId = invoice.GetProperty("id").GetGuid();
        await workspace.PostAsync("/api/invoices/process", new
        {
            invoiceId, amount = "50.0001", isValid = true, participants = new[] { "validacion", "contabilizacion", "publicacion" }
        });
        var original = await fixture.WithDatabaseAsync(db => db.JournalEntries.SingleAsync(item =>
            item.CompanyId == workspace.CompanyId && item.OperationId == invoiceId));
        await workspace.PostAsync("/api/operations/execute",
            Operation(workspace.CompanyId, Guid.NewGuid(), "reverse-voucher", original.Id, "50.0001"));

        var summary = await workspace.GetAsync("/api/reports/summary");
        Assert.Equal(0m, Amount(summary.GetProperty("receivableTotal")));
        Assert.Equal(2, summary.GetProperty("entryCount").GetInt32());
        var invoices = await workspace.GetAsync("/api/invoices");
        Assert.Equal("Reversed", Assert.Single(invoices.GetProperty("items").EnumerateArray()).GetProperty("status").GetString());
        var originalStatus = await fixture.WithDatabaseAsync(db => db.JournalEntries.Where(item => item.Id == original.Id)
            .Select(item => item.Status).SingleAsync());
        Assert.Equal("Posted", originalStatus);
    }

    private static decimal Amount(JsonElement value) => decimal.Parse(value.GetString()!, CultureInfo.InvariantCulture);

    private static object Operation(Guid companyId, Guid operationId, string kind, Guid targetId, string amount) => new
    {
        request = new { companyId, operationId, kind, targetId, amount },
        context = new { outstandingAmount = "999999.0000", targetIsPublished = false, completedOperations = Array.Empty<object>() }
    };

    private static async Task AddPostingAccounts(TestWorkspace workspace, params string[] codes)
    {
        foreach (var code in codes)
            await workspace.PostAsync("/api/accounts", new { code, name = "Cuenta " + code });
    }
}
