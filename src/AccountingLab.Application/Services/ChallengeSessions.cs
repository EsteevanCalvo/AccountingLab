using System.Collections.Concurrent;
using AccountingLab.Domain.Challenges.DP17Memento;
using AccountingLab.Domain.Challenges.DP18Observer;
using AccountingLab.Domain.Model;
using BudgetDraft = AccountingLab.Domain.Challenges.DP17Memento.BudgetDraft;

namespace AccountingLab.Application.Services;

public sealed class ChallengeSessions(TimeProvider clock)
{
    private readonly BudgetHistoryChallenge budgets = new();
    private readonly ConcurrentDictionary<Guid, SavedBudget> snapshots = new();
    private readonly ConcurrentDictionary<Guid, TenantSubscriptions> subscriptions = new();

    public Guid SaveBudget(Guid companyId, Guid userId, BudgetDraft draft)
    {
        if (draft.Id == Guid.Empty)
            throw new BusinessRuleException("El borrador necesita un identificador.");
        var snapshot = RunBudgetOperation(() => budgets.Save(draft));
        var now = clock.GetUtcNow();
        foreach (var (id, item) in snapshots)
            if (item.Expires <= now)
                snapshots.TryRemove(id, out _);
        var snapshotId = Guid.NewGuid();
        snapshots[snapshotId] = new(companyId, userId, now.AddHours(1), snapshot);
        return snapshotId;
    }

    public BudgetDraft RestoreBudget(Guid companyId, Guid userId, Guid snapshotId, BudgetDraft current)
    {
        if (!snapshots.TryGetValue(snapshotId, out var saved) || saved.Expires <= clock.GetUtcNow()
            || saved.CompanyId != companyId || saved.UserId != userId)
            throw new ResourceNotFoundException("No se encontró una instantánea vigente del usuario en esta empresa.");
        return RunBudgetOperation(() => budgets.Restore(current, saved.Snapshot));
    }

    public void Subscribe(Guid companyId, Guid userId) =>
        subscriptions.GetOrAdd(companyId, _ => new()).Subscribe(userId);

    public void Unsubscribe(Guid companyId, Guid userId)
    {
        if (subscriptions.TryGetValue(companyId, out var tenant))
            tenant.Unsubscribe(userId);
    }

    public IReadOnlyList<FinancialEvent> ReadInbox(Guid companyId, Guid userId) =>
        subscriptions.TryGetValue(companyId, out var tenant) ? tenant.ReadInbox(userId) : [];

    public IReadOnlyList<string> Publish(Guid companyId, FinancialEvent item)
    {
        if (item.EventId == Guid.Empty)
            throw new BusinessRuleException("El evento necesita un identificador.");
        BasicDataRules.Required(item.Kind, "Tipo de evento");
        MoneyRules.Validate(item.Amount, allowZero: true);
        return subscriptions.TryGetValue(companyId, out var tenant) ? tenant.Publish(item) : [];
    }

    private static T RunBudgetOperation<T>(Func<T> operation)
    {
        try { return operation(); }
        catch (InvalidOperationException error) { throw new ResourceConflictException(error.Message); }
    }

    private sealed record SavedBudget(Guid CompanyId, Guid UserId, DateTimeOffset Expires, IBudgetSnapshot Snapshot);

    private sealed class TenantSubscriptions
    {
        private readonly object gate = new();
        private readonly FinancialNotificationsChallenge publisher = new();
        private readonly Dictionary<Guid, Queue<FinancialEvent>> inboxes = new();

        internal void Subscribe(Guid userId)
        {
            lock (gate)
            {
                if (inboxes.ContainsKey(userId))
                    return;
                var inbox = new Queue<FinancialEvent>();
                publisher.Subscribe(userId.ToString("D"), item =>
                {
                    const int capacity = 100;
                    if (inbox.Count == capacity)
                        inbox.Dequeue();
                    inbox.Enqueue(item);
                });
                inboxes.Add(userId, inbox);
            }
        }

        internal void Unsubscribe(Guid userId)
        {
            lock (gate)
            {
                publisher.Unsubscribe(userId.ToString("D"));
                inboxes.Remove(userId);
            }
        }

        internal IReadOnlyList<FinancialEvent> ReadInbox(Guid userId)
        {
            lock (gate)
                return inboxes.TryGetValue(userId, out var inbox) ? inbox.ToArray() : [];
        }

        internal IReadOnlyList<string> Publish(FinancialEvent item)
        {
            lock (gate)
                return publisher.Publish(item);
        }
    }
}
