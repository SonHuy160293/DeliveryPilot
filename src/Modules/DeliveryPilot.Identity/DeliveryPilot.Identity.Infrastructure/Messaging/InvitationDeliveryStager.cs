using System.Text.Json;
using DeliveryPilot.EntityFrameworkCore.Reliability;
using DeliveryPilot.Identity.Application.Abstractions.Invitations;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.Notification.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DeliveryPilot.Identity.Infrastructure.Messaging;

public sealed class InvitationDeliveryStager(IdentityDbContext db, IOptions<InvitationDeliveryProducerOptions> settings) : IInvitationDeliveryStager
{
    public async Task StageAsync(OrganizationInvitation invitation, IReadOnlyList<OrganizationInvitationRoleOffer> offers, OrganizationInvitationDelivery delivery, InvitationProof proof, string correlationId, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null || invitation.OrganizationId != delivery.OrganizationId || invitation.Id != delivery.InvitationId || invitation.CurrentDeliveryMessageId != delivery.DeliveryMessageId || offers.Count == 0 || offers.Any(offer => offer.OrganizationId != invitation.OrganizationId || offer.InvitationId != invitation.Id))
        {
            throw new InvalidOperationException("Invitation delivery requires a scoped command transaction.");
        }

        InvitationDeliveryProducerOptions options = settings.Value;
        if (string.IsNullOrWhiteSpace(options.KeyId) || string.IsNullOrWhiteSpace(options.PublicKeyFile))
        {
            throw new InvalidOperationException("Invitation delivery protection is not configured.");
        }

        string? workspaceName = await db.Organizations.AsNoTracking().Where(x => x.Id == invitation.OrganizationId).Select(x => x.Name).SingleOrDefaultAsync(cancellationToken);
        if (workspaceName is null)
        {
            throw new InvalidOperationException("Invitation workspace is unavailable.");
        }

        Guid correlation = Guid.TryParse(correlationId, out Guid parsed) && parsed != Guid.Empty ? parsed : Guid.CreateVersion7();
        string publicKey = await File.ReadAllTextAsync(options.PublicKeyFile, cancellationToken);
        var payload = new OrganizationInvitationEmailData(invitation.RecipientEmail, workspaceName, offers.Select(x => x.OfferedRoleName).ToArray(), proof.Value);
        OrganizationInvitationEmailRequestedV1 request = InvitationEmailEnvelopeFactory.Create(delivery.DeliveryMessageId, correlation, invitation.OrganizationId, invitation.Id, delivery.IssuedAt, delivery.ExpiresAt, payload, options.KeyId, publicKey);
        db.Set<OutboxMessage>().Add(OutboxMessage.Create(request.MessageId, OrganizationInvitationEmailRequestedV1.EventType, 1, request.MessageId, 1, JsonSerializer.Serialize(request), request.CorrelationId, null, request.IssuedAt));
    }
}
