using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.Authority.Queries.MatchUserEmail;

public sealed class MatchUserEmailQueryValidator : IValidator<MatchUserEmailQuery>
{
    public Task ValidateAsync(MatchUserEmailQuery request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.UserId is null || request.UserId == Guid.Empty || string.IsNullOrWhiteSpace(request.Email) || request.Email.Trim().Length is < 3 or > 320)
        {
            throw new CqrsValidationException(["A nonempty user identifier and an email of 3 to 320 characters are required."]);
        }

        return Task.CompletedTask;
    }
}
