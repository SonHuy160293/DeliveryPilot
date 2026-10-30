using System.Net;
using System.Text.Json;
using DeliveryPilot.Identity.Contracts;
using DeliveryPilot.Notification.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace DeliveryPilot.Notification.Infrastructure.Security;

public sealed class InvitationDispatchEligibility(HttpClient client, IConfiguration configuration) : IInvitationDispatchEligibility
{
    public async Task<bool?> IsAllowedAsync(Guid deliveryId, CancellationToken cancellationToken)
    {
        string? origin = configuration["InvitationDispatch:IdentityOrigin"];
        string? keyFile = configuration["InvitationDispatch:ApiKeyFile"];
        if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? baseUri) || baseUri.UserInfo.Length != 0 || baseUri.Query.Length != 0 || baseUri.Fragment.Length != 0 || baseUri.AbsolutePath != "/" || (baseUri.Scheme != "https" && baseUri.Scheme != "http") || string.IsNullOrWhiteSpace(keyFile))
        {
            return null;
        }

        try
        {
            string key = (await File.ReadAllTextAsync(keyFile, cancellationToken)).Trim();
            if (key.Length is < 16 or > 4096)
            {
                return null;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, $"/api/v1/integrations/organization-invitation-deliveries/{deliveryId:D}/eligibility"));
            request.Headers.Add("X-Invitation-Dispatch-Key", key);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            InvitationDeliveryEligibility? eligibility = json.RootElement.GetProperty("data").Deserialize<InvitationDeliveryEligibility>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (eligibility is null || (eligibility.Allowed && (eligibility.ExpiresAt is null || eligibility.ExpiresAt.Value.Offset != TimeSpan.Zero || eligibility.ExpiresAt <= DateTimeOffset.UtcNow)))
            {
                return null;
            }

            return eligibility.Allowed;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return null;
        }
    }
}
