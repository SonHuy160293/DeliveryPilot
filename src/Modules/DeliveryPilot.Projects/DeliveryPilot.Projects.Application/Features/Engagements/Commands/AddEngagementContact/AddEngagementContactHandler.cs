using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.AddEngagementContact;

public sealed class AddEngagementContactHandler(EngagementMutationService mutations) : ICommandHandler<AddEngagementContactCommand, ContactResult>
{
    public async Task<Result<ContactResult>> HandleAsync(AddEngagementContactCommand request, CancellationToken cancellationToken)
    {
        EngagementMutationContext context = await mutations.LockAsync(request.OrganizationId, request.EngagementId, cancellationToken);
        context.Engagement.EnsureActive();
        var contact = EngagementContact.Create(request.OrganizationId, request.EngagementId, context.UserId, context.OccurredAt, request.Name!, request.Email!, request.ContactRole!);
        Guid? demotedId = contact.ContactRole == EngagementContact.ClientProductOwnerRole
            ? await mutations.DemoteClientProductOwnerAsync(context, contact.Id, cancellationToken) : null;
        mutations.AddContact(contact);
        context.Engagement.RecordChange(context.OccurredAt);
        mutations.Record(
            context,
            demotedId is null ? "contact.added" : "contact.designated-client-po",
            "Contact",
            contact.Id,
            new { previous = (object?)null, @new = ContactResult.From(contact), demoted = demotedId is null ? null : new { contactId = demotedId, previousRole = EngagementContact.ClientProductOwnerRole, newRole = EngagementContact.OtherRole } },
            null);
        return Result.Success(ContactResult.From(contact));
    }
}
