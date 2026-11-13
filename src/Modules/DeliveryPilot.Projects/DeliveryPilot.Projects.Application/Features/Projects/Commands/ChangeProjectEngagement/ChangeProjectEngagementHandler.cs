using DeliveryPilot.Projects.Application.Abstractions.Authority;
using DeliveryPilot.Projects.Application.Features.Projects.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Application.Features.Projects.Commands.ChangeProjectEngagement;

public sealed class ChangeProjectEngagementHandler(ProjectMutationService mutations, IWorkspaceAuthority authority) : ICommandHandler<ChangeProjectEngagementCommand, ProjectSettingsResult>
{
    public async Task<Result<ProjectSettingsResult>> HandleAsync(ChangeProjectEngagementCommand request, CancellationToken cancellationToken)
    {
        (ProjectMutationContext context, Engagement? engagement) = await mutations.LockProjectAsync(request.OrganizationId, request.ProjectId, ProjectPermissions.ManageAdministrators, request.EngagementId, cancellationToken);
        WorkspaceAuthority facts = await authority.RequireAsync(context.UserId, request.OrganizationId, context.SessionId, WorkspacePermissions.EngagementsManage, cancellationToken);
        Project project = context.Project;
        if (project.Status != Project.DraftStatus)
        {
            throw new ConflictException("Project.NotDraft", "The project is not Draft.");
        }

        if (request.EngagementId.HasValue && request.EngagementId != project.EngagementId)
        {
            if (engagement is null || !(engagement.CreatedByUserId == context.UserId || facts.HasPermission(WorkspacePermissions.EngagementsReadAll) || facts.HasPermission(WorkspacePermissions.EngagementsManage)))
            {
                throw new NotFoundException("Engagement.NotFound", "Engagement was not found.");
            }

            if (engagement.Status != Engagement.ActiveStatus)
            {
                throw new ConflictException("Engagement.Closed", "Engagement is closed.");
            }
        }

        string previousKind = project.Kind;
        Guid? previousEngagementId = project.EngagementId;
        if (project.ChangeEngagement(request.Kind!, request.EngagementId, request.ExpectedVersion!.Value, context.OccurredAt))
        {
            string eventType = previousKind == project.Kind ? "project.engagement-changed" : "project.kind-changed";
            mutations.Record(context, eventType, "Project", project.Id, new { previousKind, kind = project.Kind, previousEngagementId, engagementId = project.EngagementId }, request.Reason!.Trim());
        }

        return Result.Success(ProjectSettingsResult.From(project));
    }
}
