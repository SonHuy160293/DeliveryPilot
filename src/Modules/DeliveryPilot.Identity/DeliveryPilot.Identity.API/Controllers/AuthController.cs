using System.Security.Claims;
using DeliveryPilot.AspNetCore.Controllers;
using DeliveryPilot.Identity.API.Contracts.Auth;
using DeliveryPilot.Identity.Application.Features.Auth.Commands.LoginUser;
using DeliveryPilot.Identity.Application.Features.Auth.Commands.LogoutSession;
using DeliveryPilot.Identity.Application.Features.Auth.Commands.RefreshSession;
using DeliveryPilot.Identity.Application.Features.Auth.Commands.RegisterUser;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Cqrs;
using DeliveryPilot.SharedKernel.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace DeliveryPilot.Identity.API.Controllers;

public sealed class AuthController(ISender sender) : PublicApiController
{
    public const string RefreshTokenCookieName = "dp_refresh_token";
    private const int EmptyBodyLimit = 1024;

    [HttpPost("register")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken)
    {
        Result<RegisterUserResponse> result = await sender.SendAsync(
            new RegisterUserCommand(request.DisplayName, request.Email, request.Password), cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : StatusCode(StatusCodes.Status500InternalServerError);
    }

    [HttpPost("login")]
    [RequestSizeLimit(16384)]
    public async Task<IActionResult> LoginAsync(LoginUserRequest request, CancellationToken cancellationToken)
    {
        Result<LoginUserResult> result = await sender.SendAsync(
            new LoginUserCommand(request.Email, request.Password), cancellationToken);
        if (!result.IsSuccess)
        {
            return result.Error.Code == LoginUserHandler.InvalidCredentialsCode
                ? Unauthorized(result.Error)
                : StatusCode(StatusCodes.Status500InternalServerError);
        }

        SetNoStore();
        Response.Cookies.Append(RefreshTokenCookieName, result.Value.RefreshToken, RefreshCookieOptions(result.Value.RefreshTokenExpiresAt));
        return Ok(result.Value.Response);
    }

    /// <summary>
    /// Rotates the refresh cookie and returns a new access token. Any rejection is a generic 401 that also
    /// clears the cookie, so a client never retries with a dead token.
    /// </summary>
    [HttpPost("refresh")]
    [RequestSizeLimit(EmptyBodyLimit)]
    public async Task<IActionResult> RefreshAsync(CancellationToken cancellationToken)
    {
        SetNoStore();
        Response.OnStarting(() =>
        {
            if (Response.StatusCode == StatusCodes.Status401Unauthorized)
            {
                Response.Cookies.Delete(RefreshTokenCookieName, RefreshCookieOptions(expires: null));
            }

            return Task.CompletedTask;
        });

        Request.Cookies.TryGetValue(RefreshTokenCookieName, out string? refreshToken);
        Result<RefreshSessionResult> result = await sender.SendAsync(new RefreshSessionCommand(refreshToken), cancellationToken);
        if (!result.IsSuccess)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        Response.Cookies.Append(RefreshTokenCookieName, result.Value.RefreshToken, RefreshCookieOptions(result.Value.RefreshTokenExpiresAt));
        return Ok(result.Value.Response);
    }

    /// <summary>
    /// Ends the current session and always answers 204, including when nothing was left to end. Only an
    /// unexpected failure produces an error, and in that case nothing is reported as logged out.
    /// </summary>
    [HttpPost("logout")]
    [RequestSizeLimit(EmptyBodyLimit)]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        // Claims exist only when the bearer handler validated the token, including its live session check.
        bool authenticated = User.Identity?.IsAuthenticated == true;
        Guid? userId = authenticated ? ReadGuidClaim(User, JwtRegisteredClaimNames.Sub, ClaimTypes.NameIdentifier) : null;
        Guid? sessionId = authenticated ? ReadGuidClaim(User, CustomClaims.SessionId) : null;
        Request.Cookies.TryGetValue(RefreshTokenCookieName, out string? refreshToken);

        Result<bool> result = await sender.SendAsync(new LogoutSessionCommand(userId, sessionId, refreshToken), cancellationToken);
        if (!result.IsSuccess)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        SetNoStore();
        Response.Cookies.Delete(RefreshTokenCookieName, RefreshCookieOptions(expires: null));
        return new BodylessNoContentResult();
    }

    private static CookieOptions RefreshCookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        Expires = expires,
    };

    private static Guid? ReadGuidClaim(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (string claimType in claimTypes)
        {
            if (Guid.TryParse(principal.FindFirstValue(claimType), out Guid value) && value != Guid.Empty)
            {
                return value;
            }
        }

        return null;
    }

    private void SetNoStore()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
    }

    /// <summary>
    /// A true 204 for logout (D-006). The global envelope filter rewrites <see cref="NoContentResult"/>
    /// into a 200 envelope, so this result type is deliberately not one the filter recognizes.
    /// </summary>
    private sealed class BodylessNoContentResult : IActionResult
    {
        public Task ExecuteResultAsync(ActionContext context)
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        }
    }
}
