namespace AccountingLab.Domain.Challenges.DP02AbstractFactory;

public sealed record AccountingProfileRequest(string Profile, string FictionalRegime);
public sealed record AccountingFamilyMember(string Role, string CompatibilityKey);
public sealed record AccountingServiceFamily(string Profile, string FictionalRegime, IReadOnlyList<AccountingFamilyMember> Members);
public interface IAccountingProfileChallenge { AccountingServiceFamily CreateFamily(AccountingProfileRequest request); }
public sealed class AccountingProfileChallenge : IAccountingProfileChallenge
{
    public AccountingServiceFamily CreateFamily(AccountingProfileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FictionalRegime);
        FamilyFactory factory = request.Profile?.ToLowerInvariant() switch
        {
            "comercial" => new CommercialFactory(request.FictionalRegime),
            "servicios" => new ServicesFactory(request.FictionalRegime),
            _ => throw new ArgumentException("Perfil contable desconocido.", nameof(request))
        };

        return new(request.Profile!, request.FictionalRegime, Array.AsReadOnly(
            new[] { factory.CreateCalculation(), factory.CreateValidation(), factory.CreateReporting() }));
    }

    private abstract class FamilyFactory(string profile, string regime)
    {
        protected string CompatibilityKey { get; } = $"{profile}:{regime}";
        internal abstract AccountingFamilyMember CreateCalculation();
        internal abstract AccountingFamilyMember CreateValidation();
        internal abstract AccountingFamilyMember CreateReporting();
    }

    private sealed class CommercialFactory(string regime) : FamilyFactory("comercial", regime)
    {
        internal override AccountingFamilyMember CreateCalculation() => new("calculo", CompatibilityKey);
        internal override AccountingFamilyMember CreateValidation() => new("validacion", CompatibilityKey);
        internal override AccountingFamilyMember CreateReporting() => new("reporte", CompatibilityKey);
    }

    private sealed class ServicesFactory(string regime) : FamilyFactory("servicios", regime)
    {
        internal override AccountingFamilyMember CreateCalculation() => new("calculo", CompatibilityKey);
        internal override AccountingFamilyMember CreateValidation() => new("validacion", CompatibilityKey);
        internal override AccountingFamilyMember CreateReporting() => new("reporte", CompatibilityKey);
    }
}
