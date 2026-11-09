using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.SetEngagementContractValue;

public sealed class SetEngagementContractValueHandler(EngagementMutationService mutations) : ICommandHandler<SetEngagementContractValueCommand, EngagementContractValueResult>
{
    public async Task<Result<EngagementContractValueResult>> HandleAsync(SetEngagementContractValueCommand request, CancellationToken cancellationToken)
    {
        EngagementMutationContext context = await mutations.LockAsync(request.OrganizationId, request.EngagementId, cancellationToken);
        Engagement engagement = context.Engagement;
        engagement.EnsureActive();
        engagement.RequireVersion(request.ExpectedVersion!.Value);
        var previous = new { engagement.ContractAmount, engagement.ContractCurrency, engagement.PlannedEffortPersonDays };
        if (engagement.SetContractValue(request.ContractAmount, request.ContractCurrency, request.PlannedEffortPersonDays, context.OccurredAt))
        {
            var current = new { engagement.ContractAmount, engagement.ContractCurrency, engagement.PlannedEffortPersonDays };
            mutations.Record(context, "engagement.value-changed", "Engagement", engagement.Id, new { previous, @new = current }, request.Reason);
        }

        return Result.Success(new EngagementContractValueResult(engagement.Id, engagement.ContractAmount, engagement.ContractCurrency, engagement.PlannedEffortPersonDays, engagement.Version));
    }
}
