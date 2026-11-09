using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.RemoveEngagementContact;

public sealed class RemoveEngagementContactHandler(EngagementMutationService mutations) : ICommandHandler<RemoveEngagementContactCommand, ContactResult>
{
    public async Task<Result<ContactResult>> HandleAsync(RemoveEngagementContactCommand request, CancellationToken cancellationToken)
    {
        EngagementMutationContext context = await mutations.LockAsync(request.OrganizationId, request.EngagementId, cancellationToken);
        context.Engagement.EnsureActive();
        EngagementContact contact = await mutations.FindContactAsync(context, request.ContactId, cancellationToken);
        contact.RequireVersion(request.ExpectedVersion!.Value);
        ContactResult previous = ContactResult.From(contact);
        contact.Remove(context.UserId, context.OccurredAt);
        context.Engagement.RecordChange(context.OccurredAt);
        mutations.Record(context, "contact.removed", "Contact", contact.Id, new { previous, @new = (object?)null }, request.Reason);
        return Result.Success(ContactResult.From(contact));
    }
}
