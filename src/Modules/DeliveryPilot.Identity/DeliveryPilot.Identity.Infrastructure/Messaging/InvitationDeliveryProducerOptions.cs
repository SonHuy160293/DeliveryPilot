namespace DeliveryPilot.Identity.Infrastructure.Messaging;

public sealed class InvitationDeliveryProducerOptions
{
    public const string SectionName = "InvitationDeliveryProducer";
    public string KeyId { get; set; } = string.Empty;
    public string PublicKeyFile { get; set; } = string.Empty;
}
