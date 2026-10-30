using DeliveryPilot.Notification.Contracts;

namespace DeliveryPilot.Identity.Application.Abstractions.Messaging;
public interface IVerificationEmailOutbox
{
    Task StageAsync(EmailDeliveryRequestedV1 request, CancellationToken cancellationToken);
}

public interface IEmailDeliveryReceiptStore
{
    Task RecordAsync(EmailDeliveryRecordedV1 receipt, CancellationToken cancellationToken);
}
