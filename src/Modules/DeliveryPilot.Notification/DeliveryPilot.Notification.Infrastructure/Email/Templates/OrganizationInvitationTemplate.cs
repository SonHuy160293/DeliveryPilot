using System.Net;
using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;
using DeliveryPilot.Notification.Contracts;

namespace DeliveryPilot.Notification.Infrastructure.Email.Templates;

public static class OrganizationInvitationTemplate
{
    public static RenderedEmail Render(OrganizationInvitationEmailRequestedV1 request, ReadOnlySpan<byte> data, EmailDeliveryOptions options)
    {
        request.Validate();
        var payload = JsonSerializer.Deserialize<OrganizationInvitationEmailData>(data) ?? throw new ArgumentException("Invalid invitation template data.");
        if (!MailAddress.TryCreate(payload.Recipient, out var address) || address.Address != payload.Recipient || string.IsNullOrWhiteSpace(payload.WorkspaceName) || payload.WorkspaceName.Length > 160 || payload.OfferedRoleNames is null || payload.OfferedRoleNames.Count == 0 || payload.OfferedRoleNames.Any(name => string.IsNullOrWhiteSpace(name) || name.Length > 160) || payload.Proof is null || !Regex.IsMatch(payload.Proof, "^[A-Za-z0-9_-]{43}$", RegexOptions.NonBacktracking))
        {
            throw new ArgumentException("Invalid invitation template data.");
        }

        var origin = new Uri(options.FrontendOrigin, UriKind.Absolute);
        if (origin.AbsolutePath != "/" || origin.Query.Length > 0 || origin.Fragment.Length > 0 || origin.UserInfo.Length > 0 || (origin.Scheme != "https" && !(options.Development && origin.IsLoopback)))
        {
            throw new ArgumentException("Invalid frontend origin.");
        }

        string link = new Uri(origin, "/join-workspace").AbsoluteUri + "#invitationId=" + request.InvitationId.ToString("D") + "&proof=" + payload.Proof;
        string workspace = WebUtility.HtmlEncode(payload.WorkspaceName);
        string roles = string.Join(", ", payload.OfferedRoleNames.Select(WebUtility.HtmlEncode));
        string expiry = request.ExpiresAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture);
        string html = $"<!doctype html><html lang=\"en\"><body><main><h1>Workspace invitation</h1><p>You are invited to {workspace}.</p><p>Offered roles: {roles}</p><p><a href=\"{WebUtility.HtmlEncode(link)}\">Review invitation</a></p><p>Expires {expiry}.</p></main></body></html>";
        string text = $"You are invited to {payload.WorkspaceName}.\nOffered roles: {string.Join(", ", payload.OfferedRoleNames)}\nReview invitation: {link}\nExpires: {expiry}.";
        return new RenderedEmail(options.Sender, [address.Address], "Your workspace invitation", html, text);
    }
}
