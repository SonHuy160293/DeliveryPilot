using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.Authority.Queries.CheckSession;

public sealed class CheckSessionQueryValidator : IValidator<CheckSessionQuery>
{
    public Task ValidateAsync(CheckSessionQuery request, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (request.UserId == Guid.Empty)
        {
            errors.Add("UserId must not be empty.");
        }

        if (request.SessionId == Guid.Empty)
        {
            errors.Add("SessionId must not be empty.");
        }

        if (errors.Count > 0)
        {
            throw new CqrsValidationException(errors);
        }

        return Task.CompletedTask;
    }
}
