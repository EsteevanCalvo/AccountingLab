using AccountingLab.Domain.Model;

namespace AccountingLab.Domain.Challenges.DP11Flyweight;

public sealed record MovementClassificationInput(Guid Id, decimal Amount, string Code, string Label, string FictionalTaxScope);
public sealed record ClassificationMetadata(string Code, string Label, string FictionalTaxScope);
public sealed record ClassifiedMovement(Guid Id, decimal Amount, ClassificationMetadata Metadata);
public interface ISharedClassificationChallenge { IReadOnlyList<ClassifiedMovement> Prepare(IReadOnlyList<MovementClassificationInput> movements); }
public sealed class SharedClassificationChallenge : ISharedClassificationChallenge
{
    public IReadOnlyList<ClassifiedMovement> Prepare(IReadOnlyList<MovementClassificationInput> movements)
    {
        ArgumentNullException.ThrowIfNull(movements);
        var factory = new ClassificationFactory();
        var classified = new List<ClassifiedMovement>(movements.Count);
        foreach (var movement in movements)
        {
            MoneyRules.ValidateSigned(movement.Amount);
            classified.Add(new(movement.Id, movement.Amount, factory.Get(movement)));
        }
        return classified.AsReadOnly();
    }

    private sealed class ClassificationFactory
    {
        private readonly Dictionary<(string Code, string Label, string Scope), ClassificationMetadata> metadata = new();

        internal ClassificationMetadata Get(MovementClassificationInput movement)
        {
            var key = (movement.Code, movement.Label, movement.FictionalTaxScope);
            if (metadata.TryGetValue(key, out var existing))
                return existing;
            var classification = new ClassificationMetadata(key.Code, key.Label, key.FictionalTaxScope);
            metadata.Add(key, classification);
            return classification;
        }
    }
}
