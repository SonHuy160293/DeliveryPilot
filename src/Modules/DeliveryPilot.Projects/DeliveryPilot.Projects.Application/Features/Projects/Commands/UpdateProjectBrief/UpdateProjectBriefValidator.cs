using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Application.Features.Projects.Commands.UpdateProjectBrief;

public sealed class UpdateProjectBriefValidator : IValidator<UpdateProjectBriefCommand>
{
    public Task ValidateAsync(UpdateProjectBriefCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = new List<string>();
        if (request.OrganizationId == Guid.Empty || request.ProjectId == Guid.Empty || request.ExpectedVersion is null or <= 0)
        {
            errors.Add("Valid project identifiers and a positive expected version are required.");
        }

        try
        {
            request.Values.Normalize();
        }
        catch (BadRequestException error)
        {
            errors.Add(error.Message);
        }

        if (errors.Count > 0)
        {
            throw new CqrsValidationException(errors);
        }

        return Task.CompletedTask;
    }
}
