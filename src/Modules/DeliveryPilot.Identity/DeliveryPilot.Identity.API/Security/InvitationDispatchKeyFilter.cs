using System.Security.Cryptography;
using System.Text;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DeliveryPilot.Identity.API.Security;

public sealed class InvitationDispatchKeyFilter(IConfiguration configuration) : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        string? expected = configuration["InvitationDispatch:ApiKey"];
        if (string.IsNullOrWhiteSpace(expected))
        {
            context.Result = Error(StatusCodes.Status503ServiceUnavailable, "InvitationDispatch.NotConfigured", "Invitation dispatch authentication is unavailable.");
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue("X-Invitation-Dispatch-Key", out var values) || values.Count != 1)
        {
            context.Result = Error(StatusCodes.Status401Unauthorized, "InvitationDispatch.Unauthorized", "Invitation dispatch authentication failed.");
            return;
        }

        string? provided = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(provided) || provided.Length > 4096)
        {
            context.Result = Error(StatusCodes.Status401Unauthorized, "InvitationDispatch.Unauthorized", "Invitation dispatch authentication failed.");
            return;
        }

        byte[] actualHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        byte[] expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        if (!CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
        {
            context.Result = Error(StatusCodes.Status401Unauthorized, "InvitationDispatch.Unauthorized", "Invitation dispatch authentication failed.");
        }
    }

    private static ObjectResult Error(int status, string code, string message)
        => new(new ApiErrorResponse(code, message)) { StatusCode = status };
}
