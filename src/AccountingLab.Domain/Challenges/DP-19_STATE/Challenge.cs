using AccountingLab.Domain.Model;

namespace AccountingLab.Domain.Challenges.DP19State;

public sealed record InvoiceLifecycleRequest(string CurrentState, string Action, decimal OutstandingAmount, DateOnly DueDate, DateOnly BusinessDate);
public sealed record InvoiceTransitionResult(string State, bool PaymentAllowed);
public interface IInvoiceLifecycleChallenge { InvoiceTransitionResult Transition(InvoiceLifecycleRequest request); }
public sealed class InvoiceLifecycleChallenge : IInvoiceLifecycleChallenge
{
    private static readonly IReadOnlyDictionary<string, InvoiceState> States =
        new Dictionary<string, InvoiceState>(StringComparer.OrdinalIgnoreCase)
        {
            ["draft"] = new DraftState(), ["issued"] = new IssuedState(),
            ["overdue"] = new OverdueState(), ["paid"] = new PaidState()
        };

    public InvoiceTransitionResult Transition(InvoiceLifecycleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        MoneyRules.Validate(request.OutstandingAmount, allowZero: true);
        if (!States.TryGetValue(request.CurrentState, out var state))
            throw new ArgumentException("Estado de factura desconocido.", nameof(request));
        var next = request.Action?.ToLowerInvariant() switch
        {
            "emit" => state.Emit(),
            "pay" => state.Pay(request.OutstandingAmount),
            "mark-overdue" => state.MarkOverdue(request.BusinessDate, request.DueDate),
            _ => throw new ArgumentException("Acción de factura desconocida.", nameof(request))
        };
        return new(next.Name, next.PaymentAllowed);
    }

    private abstract class InvoiceState(string name, bool paymentAllowed)
    {
        internal string Name { get; } = name;
        internal bool PaymentAllowed { get; } = paymentAllowed;

        internal virtual InvoiceState Emit() => Reject("emitir");
        internal virtual InvoiceState Pay(decimal outstanding) => Reject("pagar");
        internal virtual InvoiceState MarkOverdue(DateOnly businessDate, DateOnly dueDate) => Reject("marcar como vencida");

        private InvoiceState Reject(string action) =>
            throw new InvalidOperationException($"No se puede {action} una factura en estado {Name}.");
    }

    private sealed class DraftState() : InvoiceState("draft", false)
    {
        internal override InvoiceState Emit() => States["issued"];
    }

    private abstract class CollectibleState(string name) : InvoiceState(name, true)
    {
        internal override InvoiceState Pay(decimal outstanding) => outstanding == 0m
            ? States["paid"]
            : throw new InvalidOperationException("La factura conserva un saldo pendiente.");
    }

    private sealed class IssuedState() : CollectibleState("issued")
    {
        internal override InvoiceState MarkOverdue(DateOnly businessDate, DateOnly dueDate) =>
            businessDate > dueDate ? States["overdue"] : this;
    }

    private sealed class OverdueState() : CollectibleState("overdue");
    private sealed class PaidState() : InvoiceState("paid", false);
}
