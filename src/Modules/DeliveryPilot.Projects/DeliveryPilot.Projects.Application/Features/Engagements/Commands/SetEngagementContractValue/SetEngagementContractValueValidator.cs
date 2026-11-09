using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.SetEngagementContractValue;

public sealed class SetEngagementContractValueValidator : IValidator<SetEngagementContractValueCommand>
{
    public Task ValidateAsync(SetEngagementContractValueCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var errors = new List<string>();
        if (request.OrganizationId == Guid.Empty || request.EngagementId == Guid.Empty || request.ExpectedVersion is null or <= 0)
        {
            errors.Add("Organization, engagement and a positive expectedVersion are required.");
        }

        try
        {
            decimal? amount = EngagementFields.ContractAmount(request.ContractAmount);
            string? currency = EngagementFields.CurrencyCode(request.ContractCurrency);
            EngagementFields.PlannedEffortPersonDays(request.PlannedEffortPersonDays);
            EngagementFields.OptionalText(request.Reason, 2000, nameof(request.Reason));
            if (amount is not null && currency is null)
            {
                errors.Add("Contract currency is required when an amount is present.");
            }
        }
        catch (BadRequestException exception)
        {
            errors.Add(exception.Message);
        }

        if (errors.Count > 0)
        {
            throw new CqrsValidationException(errors);
        }

        return Task.CompletedTask;
    }
}
