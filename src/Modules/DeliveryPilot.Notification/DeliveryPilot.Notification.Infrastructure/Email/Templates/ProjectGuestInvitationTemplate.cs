using System.Net;
using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;
using DeliveryPilot.Notification.Contracts;

namespace DeliveryPilot.Notification.Infrastructure.Email.Templates;

public static class ProjectGuestInvitationTemplate
{
    public static RenderedEmail Render(ProjectGuestInvitationEmailRequestedV1 request, ReadOnlySpan<byte> data, EmailDeliveryOptions options)
    {
        request.Validate();
        var payload = JsonSerializer.Deserialize<ProjectGuestInvitationEmailData>(data) ?? throw new ArgumentException("Invalid project invitation template data.");
        if (!MailAddress.TryCreate(payload.Recipient, out var address) || address.Address != payload.Recipient || string.IsNullOrWhiteSpace(payload.ProjectName) || payload.ProjectName.Length > 160 || string.IsNullOrWhiteSpace(payload.OfferedRoleName) || payload.OfferedRoleName.Length > 160 || string.IsNullOrWhiteSpace(payload.InviterDisplayName) || payload.InviterDisplayName.Length > 160 || payload.Proof is null || !Regex.IsMatch(payload.Proof, "^[A-Za-z0-9_-]{43}$", RegexOptions.NonBacktracking)) { throw new ArgumentException("Invalid project invitation template data."); }
        var origin = new Uri(options.FrontendOrigin, UriKind.Absolute);
        if (origin.AbsolutePath != "/" || origin.Query.Length > 0 || origin.Fragment.Length > 0 || origin.UserInfo.Length > 0 || (origin.Scheme != "https" && !(options.Development && origin.IsLoopback))) { throw new ArgumentException("Invalid frontend origin."); }
        string link = new Uri(origin, "/join-project").AbsoluteUri + "#projectId=" + request.ProjectId.ToString("D") + "&invitationId=" + request.InvitationId.ToString("D") + "&proof=" + payload.Proof;
        string expiry = request.ExpiresAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture);
        string html = $"<!doctype html><html lang=\"en\"><body><main><h1>Project invitation</h1><p>{WebUtility.HtmlEncode(payload.InviterDisplayName)} invited you to {WebUtility.HtmlEncode(payload.ProjectName)}.</p><p>Offered role: {WebUtility.HtmlEncode(payload.OfferedRoleName)}</p><p><a href=\"{WebUtility.HtmlEncode(link)}\">Review invitation</a></p><p>Expires {expiry}.</p></main></body></html>";
        string text = $"{payload.InviterDisplayName} invited you to {payload.ProjectName}.\nOffered role: {payload.OfferedRoleName}\nReview invitation: {link}\nExpires: {expiry}.";
        return new RenderedEmail(options.Sender, [address.Address], "Your project invitation", html, text);
    }
}
