using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Application.Features.Projects.Commands.CreateProject;

public sealed record ProjectCreationDetails(string Name, string Purpose, string Kind, Guid? EngagementId)
{
    public static ProjectCreationDetails Normalize(CreateProjectCommand request)
    {
        if (request.Kind is not (Project.ClientKind or Project.InternalKind) || (request.Kind == Project.ClientKind && request.EngagementId is null) || (request.Kind == Project.InternalKind && request.EngagementId is not null))
        {
            throw new BadRequestException("Project.InvalidKind", "Client projects require an engagement; internal projects cannot have one.");
        }

        return new ProjectCreationDetails(ProjectFields.RequiredText(request.Name, 160, nameof(request.Name)), ProjectFields.RequiredText(request.Purpose, 500, nameof(request.Purpose)), request.Kind, ProjectFields.OptionalId(request.EngagementId, nameof(request.EngagementId)));
    }

    public override string ToString() => nameof(ProjectCreationDetails);
}
