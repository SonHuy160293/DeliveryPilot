using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationAudit.Queries.ListOrganizationAuditEvents;

public sealed class ListOrganizationAuditEventsValidator : IValidator<ListOrganizationAuditEventsQuery>
{
    public Task ValidateAsync(ListOrganizationAuditEventsQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AuditEventFilter filter = AuditEventFilter.Parse(request);
        AuditEventCursor.Decode(request.Cursor, filter);
        return Task.CompletedTask;
    }
}
