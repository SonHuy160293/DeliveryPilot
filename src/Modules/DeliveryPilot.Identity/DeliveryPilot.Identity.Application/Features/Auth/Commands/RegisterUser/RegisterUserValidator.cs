using System.ComponentModel.DataAnnotations;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.Auth.Commands.RegisterUser;

public sealed class RegisterUserValidator : IValidator<RegisterUserCommand>
{
    public Task ValidateAsync(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = new List<string>();
        string name = request.DisplayName?.Trim() ?? string.Empty;
        string email = request.Email?.Trim() ?? string.Empty;
        if (name.Length is < 1 or > 160) errors.Add("DisplayName must contain between 1 and 160 characters.");
        if (email.Length is < 3 or > 320 || !new EmailAddressAttribute().IsValid(email)) errors.Add("Email must be valid and contain between 3 and 320 characters.");
        if (string.IsNullOrWhiteSpace(request.Password)) errors.Add("Password is required.");
        if (errors.Count > 0) throw new CqrsValidationException(errors);
        return Task.CompletedTask;
    }
}
