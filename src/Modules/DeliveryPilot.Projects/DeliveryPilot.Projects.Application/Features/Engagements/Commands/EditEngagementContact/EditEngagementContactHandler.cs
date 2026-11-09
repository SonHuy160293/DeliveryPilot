using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.EditEngagementContact;

public sealed class EditEngagementContactHandler(EngagementMutationService mutations) : ICommandHandler<EditEngagementContactCommand, ContactResult>
{
    public async Task<Result<ContactResult>> HandleAsync(EditEngagementContactCommand request, CancellationToken cancellationToken)
    {
        EngagementMutationContext context = await mutations.LockAsync(request.OrganizationId, request.EngagementId, cancellationToken);
        context.Engagement.EnsureActive();
        EngagementContact contact = await mutations.FindContactAsync(context, request.ContactId, cancellationToken);
        contact.RequireVersion(request.ExpectedVersion!.Value);
        ContactResult previous = ContactResult.From(contact);
        if (contact.Edit(request.Name, request.Email, request.ContactRole, context.OccurredAt))
        {
            bool designation = previous.ContactRole != EngagementContact.ClientProductOwnerRole && contact.ContactRole == EngagementContact.ClientProductOwnerRole;
            Guid? demotedId = designation ? await mutations.DemoteClientProductOwnerAsync(context, contact.Id, cancellationToken) : null;
            context.Engagement.RecordChange(context.OccurredAt);
            mutations.Record(
                context,
                designation ? "contact.designated-client-po" : "contact.changed",
                "Contact",
                contact.Id,
                new { previous, @new = ContactResult.From(contact), demoted = demotedId is null ? null : new { contactId = demotedId, previousRole = EngagementContact.ClientProductOwnerRole, newRole = EngagementContact.OtherRole } },
                null);
        }

        return Result.Success(ContactResult.From(contact));
    }
}
