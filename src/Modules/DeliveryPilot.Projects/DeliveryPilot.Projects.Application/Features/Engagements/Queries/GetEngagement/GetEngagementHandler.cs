using DeliveryPilot.Projects.Application.Abstractions.Identity;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Queries.GetEngagement;

public sealed class GetEngagementHandler(EngagementReadAuthorization authorization, IEngagementReadStore store, IUserDirectory users) : IQueryHandler<GetEngagementQuery, EngagementView>
{
    public async Task<Result<EngagementView>> HandleAsync(GetEngagementQuery request, CancellationToken cancellationToken)
    {
        ReadAccess access = await authorization.RequireAsync(request.OrganizationId, cancellationToken);
        EngagementView view = await store.GetAsync(request.OrganizationId, request.EngagementId, access, cancellationToken)
            ?? throw new NotFoundException("Engagement.NotFound", "The engagement was not found.");
        if (view is EngagementDetail full)
        {
            IReadOnlyList<EngagementProjectView> projects = await store.ListProjectsAsync(request.OrganizationId, request.EngagementId, cancellationToken);
            Guid[] ids = projects.SelectMany(project => project.Administrators).Select(admin => admin.UserId).Distinct().ToArray();
            IReadOnlyDictionary<Guid, string> names = ids.Length == 0
                ? new Dictionary<Guid, string>()
                : await users.LookupDisplayNamesAsync(ids, cancellationToken);
            view = full with
            {
                Projects = projects.Select(project => project with
                {
                    Administrators = project.Administrators
                        .Select(admin => admin with { DisplayName = names.GetValueOrDefault(admin.UserId) })
                        .OrderBy(admin => admin.DisplayName, StringComparer.Ordinal).ThenBy(admin => admin.UserId).ToArray(),
                }).ToArray(),
            };
        }

        return Result.Success(view);
    }
}
