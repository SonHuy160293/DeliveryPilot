using System.Security.Cryptography;
using System.Text.Json;
using DeliveryPilot.Messaging.Security;
using DeliveryPilot.Notification.Contracts;

namespace DeliveryPilot.Identity.Infrastructure.Messaging;

public static class InvitationEmailEnvelopeFactory
{
    public static OrganizationInvitationEmailRequestedV1 Create(Guid messageId, Guid correlationId, Guid organizationId, Guid invitationId, DateTimeOffset issuedAt, DateTimeOffset expiresAt, OrganizationInvitationEmailData data, string keyId, string publicKeyPem)
    {
        var blank = new EncryptedEmailPayload(1, keyId, string.Empty, string.Empty, string.Empty, string.Empty);
        var request = new OrganizationInvitationEmailRequestedV1(messageId, correlationId, null, "identity", organizationId, invitationId, issuedAt, expiresAt, blank);
        byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(data);
        try
        {
            CipherEnvelope encrypted = EnvelopeEncryption.Encrypt(plaintext, request.AssociatedData(), publicKeyPem);
            request = request with { Payload = new EncryptedEmailPayload(1, keyId, encrypted.WrappedKey, encrypted.Nonce, encrypted.Tag, encrypted.Ciphertext) };
            request.Validate();
            return request;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }
}
