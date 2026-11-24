using AccountingLab.Domain.Model;

namespace AccountingLab.Domain.Challenges.DP08Composite;

public sealed record AccountHierarchy(string Code, bool AllowsPosting, decimal OwnBalance, IReadOnlyList<AccountHierarchy> Children);
public interface IAccountTreeChallenge { decimal Total(AccountHierarchy account); }
public sealed class AccountTreeChallenge : IAccountTreeChallenge
{
    public decimal Total(AccountHierarchy account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return CreateComponent(account, new(ReferenceEqualityComparer.Instance)).Balance;
    }

    private static IAccountComponent CreateComponent(AccountHierarchy account, HashSet<AccountHierarchy> path)
    {
        if (!path.Add(account))
            throw new ArgumentException("La jerarquía contiene un ciclo.", nameof(account));

        IAccountComponent component;
        if (account.AllowsPosting)
        {
            if (account.Children.Count != 0)
                throw new ArgumentException("Una cuenta de movimiento no admite descendientes.", nameof(account));
            MoneyRules.ValidateSigned(account.OwnBalance);
            component = new PostingAccount(account.OwnBalance);
        }
        else
        {
            if (account.OwnBalance != 0m)
                throw new ArgumentException("Una agrupadora no admite saldo propio.", nameof(account));
            component = new AccountGroup(account.Children.Select(child => CreateComponent(child, path)).ToArray());
        }
        path.Remove(account);
        return component;
    }

    private interface IAccountComponent
    {
        decimal Balance { get; }
    }

    private sealed record PostingAccount(decimal Balance) : IAccountComponent;

    private sealed class AccountGroup(IReadOnlyList<IAccountComponent> children) : IAccountComponent
    {
        public decimal Balance => children.Sum(child => child.Balance);
    }
}
