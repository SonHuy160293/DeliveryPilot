using DeliveryPilot.Projects.Application.Features.Projects.Mutations;
using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.Application.Features.Projects.Commands.ChangeSprintLength;

public sealed class ChangeSprintLengthHandler(ProjectMutationService mutations) : ICommandHandler<ChangeSprintLengthCommand, ProjectSettingsResult>
{
    public async Task<Result<ProjectSettingsResult>> HandleAsync(ChangeSprintLengthCommand request, CancellationToken cancellationToken)
    {
        (ProjectMutationContext context, _) = await mutations.LockProjectAsync(request.OrganizationId, request.ProjectId, ProjectPermissions.EditCadence, null, cancellationToken);
        Project project = context.Project;
        short previous = project.SprintLengthWeeks;
        if (project.ChangeSprintLength(request.SprintLengthWeeks!.Value, request.ExpectedVersion!.Value, context.OccurredAt))
        {
            mutations.Record(context, "project.cadence-changed", "Project", project.Id, new { previous, next = project.SprintLengthWeeks });
        }

        return Result.Success(ProjectSettingsResult.From(project));
    }
}
