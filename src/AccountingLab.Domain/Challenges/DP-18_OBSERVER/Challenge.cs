namespace AccountingLab.Domain.Challenges.DP18Observer;

public sealed record FinancialEvent(Guid EventId, string Kind, decimal Amount);
public interface IFinancialNotificationsChallenge
{
    void Subscribe(string subscriberId, Action<FinancialEvent> receive);
    void Unsubscribe(string subscriberId);
    IReadOnlyList<string> Publish(FinancialEvent financialEvent);
}
public sealed class FinancialNotificationsChallenge : IFinancialNotificationsChallenge
{
    private readonly object gate = new();
    private readonly Dictionary<string, Action<FinancialEvent>> subscriptions = new(StringComparer.Ordinal);

    public void Subscribe(string subscriberId, Action<FinancialEvent> receive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriberId);
        ArgumentNullException.ThrowIfNull(receive);
        lock (gate)
            if (!subscriptions.TryAdd(subscriberId, receive))
                throw new ArgumentException("Ya existe una suscripción con esa identidad.", nameof(subscriberId));
    }

    public void Unsubscribe(string subscriberId)
    {
        lock (gate)
            subscriptions.Remove(subscriberId);
    }

    public IReadOnlyList<string> Publish(FinancialEvent financialEvent)
    {
        ArgumentNullException.ThrowIfNull(financialEvent);
        KeyValuePair<string, Action<FinancialEvent>>[] observers;
        lock (gate)
            observers = subscriptions.ToArray();

        var delivered = new List<string>(observers.Length);
        var failures = new List<Exception>();
        foreach (var (id, receive) in observers)
        {
            try
            {
                receive(financialEvent);
                delivered.Add(id);
            }
            catch (Exception error)
            {
                failures.Add(error);
            }
        }
        if (failures.Count > 0)
            throw new AggregateException("Uno o más suscriptores no completaron la recepción.", failures);
        return delivered.AsReadOnly();
    }
}
