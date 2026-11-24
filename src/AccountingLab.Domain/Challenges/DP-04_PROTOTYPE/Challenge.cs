using AccountingLab.Domain.Model;

namespace AccountingLab.Domain.Challenges.DP04Prototype;

public sealed class TemplateLine
{
    public string AccountCode { get; set; } = "";
    public decimal Amount { get; set; }
    public List<string> Tags { get; set; } = new();

    internal TemplateLine Copy() => new() { AccountCode = AccountCode, Amount = Amount, Tags = new(Tags) };
}
public sealed class RecurringTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public List<TemplateLine> Lines { get; set; } = new();

    internal RecurringTemplate CopyWithNewIdentity() => new()
    {
        Id = Guid.NewGuid(),
        Name = Name,
        Lines = Lines.Select(line => line.Copy()).ToList()
    };
}
public interface ITemplateCopyChallenge { RecurringTemplate Copy(RecurringTemplate source); }
public sealed class TemplateCopyChallenge : ITemplateCopyChallenge
{
    public RecurringTemplate Copy(RecurringTemplate source)
    {
        ArgumentNullException.ThrowIfNull(source);
        foreach (var line in source.Lines)
            MoneyRules.ValidateSigned(line.Amount);
        return source.CopyWithNewIdentity();
    }
}
