using System.Text.Json;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Application.Features.Engagements.ClientLinks;

public sealed record RemoveClientLinkCommand(Guid OrganizationId, Guid EngagementId, string? Reason) : ICommand<LinkedEngagementRemoval>
{
    public override string ToString() => nameof(RemoveClientLinkCommand);
}

public sealed record LinkedEngagementRemoval(Guid Id);

public sealed class RemoveClientLinkHandler(ClientLinkAuthorization authorization, IClientLinkStore store, TimeProvider timeProvider) : ICommandHandler<RemoveClientLinkCommand, LinkedEngagementRemoval>
{
    public async Task<Result<LinkedEngagementRemoval>> HandleAsync(RemoveClientLinkCommand request, CancellationToken cancellationToken)
    {
        Guid userId = await authorization.RequireAsync(request.OrganizationId, cancellationToken);
        Engagement? engagement = await store.LockLinkedAsync(request.OrganizationId, request.EngagementId, cancellationToken);
        await authorization.RequireAsync(request.OrganizationId, cancellationToken);
        if (engagement is null || engagement.ClientOrganizationId != request.OrganizationId)
        {
            throw new NotFoundException("EngagementLink.NotFound", "The engagement link was not found.");
        }

        DateTimeOffset now = timeProvider.GetUtcNow().ToUniversalTime();
        engagement.RemoveClientLink(now);
        store.AddEvent(EngagementEvent.Create(
            engagement.OrganizationId,
            engagement.Id,
            "link.removed",
            "Engagement",
            engagement.Id,
            userId,
            request.OrganizationId,
            now,
            engagement.Version,
            JsonSerializer.Serialize(new { clientOrganizationId = request.OrganizationId }),
            request.Reason));
        return Result.Success(new LinkedEngagementRemoval(engagement.Id));
    }
}

public sealed class RemoveClientLinkValidator : IValidator<RemoveClientLinkCommand>
{
    public Task ValidateAsync(RemoveClientLinkCommand request, CancellationToken cancellationToken)
        => Mutations.MutationValidation.ValidateAsync(
            request.OrganizationId,
            request.EngagementId,
            null,
            null,
            false,
            () => EngagementFields.OptionalText(request.Reason, 2000, nameof(request.Reason)),
            cancellationToken);
}
