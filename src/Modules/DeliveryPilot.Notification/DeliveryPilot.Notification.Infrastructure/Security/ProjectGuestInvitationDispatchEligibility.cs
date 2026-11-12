using System.Net;
using System.Text.Json;
using DeliveryPilot.Notification.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace DeliveryPilot.Notification.Infrastructure.Security;

public sealed class ProjectGuestInvitationDispatchEligibility(HttpClient client, IConfiguration configuration) : IProjectGuestInvitationDispatchEligibility
{
    public async Task<bool?> IsAllowedAsync(Guid deliveryId, CancellationToken cancellationToken)
    {
        string? origin = configuration["ProjectGuestInvitationDispatch:ProjectsOrigin"];
        string? keyFile = configuration["ProjectGuestInvitationDispatch:ApiKeyFile"];
        if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? baseUri) || baseUri.UserInfo.Length != 0 || baseUri.Query.Length != 0 || baseUri.Fragment.Length != 0 || baseUri.AbsolutePath != "/" || (baseUri.Scheme != "https" && baseUri.Scheme != "http") || string.IsNullOrWhiteSpace(keyFile)) { return null; }
        try
        {
            string key = (await File.ReadAllTextAsync(keyFile, cancellationToken)).Trim();
            if (key.Length is < 16 or > 4096) { return null; }
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, $"/api/v1/integrations/project-guest-invitation-deliveries/{deliveryId:D}/eligibility"));
            request.Headers.Add("X-Invitation-Dispatch-Key", key);
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK) { return null; }
            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            JsonElement data = json.RootElement.GetProperty("data");
            if (!data.TryGetProperty("allowed", out JsonElement allowed) || allowed.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) { return null; }
            if (!data.TryGetProperty("expiresAt", out JsonElement expiry)) { return null; }
            if (!allowed.GetBoolean())
            {
                return expiry.ValueKind == JsonValueKind.Null || (expiry.ValueKind == JsonValueKind.String && expiry.TryGetDateTimeOffset(out DateTimeOffset deniedExpiry) && deniedExpiry.Offset == TimeSpan.Zero) ? false : null;
            }
            if (expiry.ValueKind != JsonValueKind.String || !expiry.TryGetDateTimeOffset(out DateTimeOffset expiresAt) || expiresAt.Offset != TimeSpan.Zero || expiresAt <= DateTimeOffset.UtcNow) { return null; }
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException) { return null; }
    }
}
