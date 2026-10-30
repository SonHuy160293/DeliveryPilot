using System.ComponentModel.DataAnnotations;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.Auth.Commands.LoginUser;

public sealed class LoginUserValidator : IValidator<LoginUserCommand>
{
    public const int MaxPasswordLength = 1024;

    public Task ValidateAsync(LoginUserCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = new List<string>();
        string email = request.Email?.Trim() ?? string.Empty;
        if (email.Length is < 3 or > 320 || !new EmailAddressAttribute().IsValid(email))
        {
            errors.Add("Email must be valid and contain between 3 and 320 characters.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors.Add("Password is required.");
        }
        else if (request.Password.Length > MaxPasswordLength)
        {
            errors.Add($"Password must not exceed {MaxPasswordLength} characters.");
        }

        if (errors.Count > 0)
        {
            throw new CqrsValidationException(errors);
        }

        return Task.CompletedTask;
    }
}
