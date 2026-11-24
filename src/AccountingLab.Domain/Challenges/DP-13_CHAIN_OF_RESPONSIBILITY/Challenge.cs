using AccountingLab.Domain.Model;

namespace AccountingLab.Domain.Challenges.DP13ChainOfResponsibility;

public sealed record PaymentApprovalRequest(decimal Amount, decimal AvailableFunds, bool PolicyAllows, decimal AuthorizationLimit, IReadOnlyList<string> Checks);
public sealed record PaymentApprovalDecision(bool Approved, string? RejectedBy, IReadOnlyList<string> VisitedChecks);
public interface IPaymentApprovalChallenge { PaymentApprovalDecision Evaluate(PaymentApprovalRequest request); }
public sealed class PaymentApprovalChallenge : IPaymentApprovalChallenge
{
    public PaymentApprovalDecision Evaluate(PaymentApprovalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        MoneyRules.Validate(request.Amount, allowZero: true);
        MoneyRules.Validate(request.AvailableFunds, allowZero: true);
        MoneyRules.Validate(request.AuthorizationLimit, allowZero: true);

        ApprovalCheck? chain = null;
        foreach (var name in request.Checks.Reverse())
        {
            chain = name?.ToLowerInvariant() switch
            {
                "fondos" => new FundsCheck(name, chain),
                "politica" => new PolicyCheck(name, chain),
                "limite" => new LimitCheck(name, chain),
                _ => throw new ArgumentException("Control de aprobación desconocido.", nameof(request))
            };
        }

        var visited = new List<string>();
        var rejectedBy = chain?.Evaluate(request, visited);
        return new(rejectedBy is null, rejectedBy, visited.AsReadOnly());
    }

    private abstract class ApprovalCheck(string name, ApprovalCheck? next)
    {
        internal string? Evaluate(PaymentApprovalRequest request, List<string> visited)
        {
            visited.Add(name);
            return Allows(request) ? next?.Evaluate(request, visited) : name;
        }

        protected abstract bool Allows(PaymentApprovalRequest request);
    }

    private sealed class FundsCheck(string name, ApprovalCheck? next) : ApprovalCheck(name, next)
    {
        protected override bool Allows(PaymentApprovalRequest request) => request.Amount <= request.AvailableFunds;
    }

    private sealed class PolicyCheck(string name, ApprovalCheck? next) : ApprovalCheck(name, next)
    {
        protected override bool Allows(PaymentApprovalRequest request) => request.PolicyAllows;
    }

    private sealed class LimitCheck(string name, ApprovalCheck? next) : ApprovalCheck(name, next)
    {
        protected override bool Allows(PaymentApprovalRequest request) => request.Amount <= request.AuthorizationLimit;
    }
}
