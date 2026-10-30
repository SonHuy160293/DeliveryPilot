namespace DeliveryPilot.Identity.Application.Features.Auth.Commands.LoginUser;

public sealed record LoginUserResponse(
    Guid UserId,
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt)
{
    public override string ToString()
        => $"{nameof(LoginUserResponse)} {{ UserId = {UserId}, TokenType = {TokenType}, ExpiresAt = {ExpiresAt:O} }}";
}
