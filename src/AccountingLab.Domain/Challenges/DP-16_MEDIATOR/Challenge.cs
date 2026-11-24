using AccountingLab.Domain.Model;

namespace AccountingLab.Domain.Challenges.DP16Mediator;

public sealed record InvoiceProcessingRequest(Guid InvoiceId, decimal Amount, bool IsValid, IReadOnlyList<string> Participants);
public sealed record InvoiceProcessingResult(Guid InvoiceId, bool Published, IReadOnlyList<string> ParticipantsInvoked);
public interface IInvoiceCoordinationChallenge { InvoiceProcessingResult Process(InvoiceProcessingRequest request); }
public interface IInvoiceProcessingEffects
{
    void Post(InvoiceProcessingRequest request);
    void Publish(InvoiceProcessingRequest request);
}

public sealed class InvoiceCoordinationChallenge : IInvoiceCoordinationChallenge
{
    public InvoiceProcessingResult Process(InvoiceProcessingRequest request) => Process(request, null);

    public InvoiceProcessingResult Process(InvoiceProcessingRequest request, IInvoiceProcessingEffects? effects)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new InvoiceMediator(request, RegisterParticipants(request.Participants), effects).Run();
    }

    private static Dictionary<Step, IInvoiceParticipant> RegisterParticipants(IReadOnlyList<string> names)
    {
        var participants = new Dictionary<Step, IInvoiceParticipant>();
        foreach (var name in names)
        {
            var step = name?.ToLowerInvariant() switch
            {
                "validacion" => Step.Validation,
                "contabilizacion" => Step.Posting,
                "publicacion" => Step.Publication,
                _ => throw new ArgumentException("Participante desconocido.", nameof(names))
            };
            IInvoiceParticipant participant = step switch
            {
                Step.Validation => new InvoiceValidator(name!),
                Step.Posting => new InvoicePosting(name!),
                _ => new InvoicePublication(name!)
            };
            if (!participants.TryAdd(step, participant))
                throw new ArgumentException("El participante ya fue registrado.", nameof(names));
        }
        if (participants.Count != 3)
            throw new ArgumentException("Se requieren validación, contabilización y publicación.", nameof(names));
        return participants;
    }

    private enum Step { Validation, Posting, Publication }

    private interface IInvoiceParticipant
    {
        string Name { get; }
        void Perform(InvoiceMediator mediator);
    }

    private sealed class InvoiceMediator(InvoiceProcessingRequest request, Dictionary<Step, IInvoiceParticipant> participants,
        IInvoiceProcessingEffects? effects)
    {
        private readonly List<string> invoked = [];
        private bool published;
        internal InvoiceProcessingRequest Request => request;

        internal InvoiceProcessingResult Run()
        {
            Invoke(Step.Validation);
            return new(request.InvoiceId, published, invoked.AsReadOnly());
        }

        private void Invoke(Step step)
        {
            var participant = participants[step];
            invoked.Add(participant.Name);
            participant.Perform(this);
        }

        internal void Validated() => Invoke(Step.Posting);
        internal void Posted()
        {
            effects?.Post(request);
            Invoke(Step.Publication);
        }

        internal void Published()
        {
            effects?.Publish(request);
            published = true;
        }
    }

    private sealed record InvoiceValidator(string Name) : IInvoiceParticipant
    {
        public void Perform(InvoiceMediator mediator)
        {
            if (mediator.Request.IsValid && mediator.Request.InvoiceId != Guid.Empty && mediator.Request.Amount > 0)
                mediator.Validated();
        }
    }

    private sealed record InvoicePosting(string Name) : IInvoiceParticipant
    {
        public void Perform(InvoiceMediator mediator)
        {
            MoneyRules.Validate(mediator.Request.Amount);
            mediator.Posted();
        }
    }

    private sealed record InvoicePublication(string Name) : IInvoiceParticipant
    {
        public void Perform(InvoiceMediator mediator) => mediator.Published();
    }
}
