using DeliveryPilot.AspNetCore.Controllers;
using DeliveryPilot.Identity.API.Contracts.Auth;
using DeliveryPilot.Identity.Application.Features.Auth.Commands.LoginUser;
using DeliveryPilot.Identity.Application.Features.Auth.Commands.RegisterUser;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Cqrs;
using Microsoft.AspNetCore.Mvc;

namespace DeliveryPilot.Identity.API.Controllers;

public sealed class AuthController(ISender sender) : PublicApiController
{
    public const string RefreshTokenCookieName = "dp_refresh_token";

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

        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        Response.Cookies.Append(
            RefreshTokenCookieName,
            result.Value.RefreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                Expires = result.Value.RefreshTokenExpiresAt,
            });

        return Ok(result.Value.Response);
    }
}
