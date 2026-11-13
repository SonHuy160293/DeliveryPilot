using DeliveryPilot.Projects.Application.Features.Projects.Mutations;
using DeliveryPilot.Projects.Application.Features.Projects.Queries;
using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.Application.Features.Projects.Commands.UpdateProjectBrief;

public sealed class UpdateProjectBriefHandler(ProjectMutationService mutations) : ICommandHandler<UpdateProjectBriefCommand, ProjectBriefView>
{
    public async Task<Result<ProjectBriefView>> HandleAsync(UpdateProjectBriefCommand request, CancellationToken cancellationToken)
    {
        (ProjectMutationContext context, ProjectBrief brief) = await mutations.LockBriefAsync(request.OrganizationId, request.ProjectId, ProjectPermissions.EditBrief, cancellationToken);
        IReadOnlyDictionary<string, ProjectBriefChange> changes = brief.Update(request.Values, request.ExpectedVersion!.Value, context.UserId, context.OccurredAt);
        if (changes.Count > 0)
        {
            string eventType = changes.ContainsKey("productGoal") ? "brief.product-goal-changed" : "brief.changed";
            mutations.Record(context, eventType, "Brief", brief.ProjectId, changes);
        }

        return Result.Success(ProjectBriefView.From(brief, true));
    }
}
