using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Application.Features.Projects.Queries;

public static class ProjectQueryParameters
{
    public static string Status(string? value)
    {
        string status = value?.Trim().ToLowerInvariant() ?? "all";
        if (status is not ("all" or "active" or "draft")) { throw new BadRequestException("Project.InvalidStatus", "The project status filter is invalid."); }
        return status;
    }
}
