using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Domain.Models.Projects;

public static class ProjectFields
{
    public static string RequiredText(string? value, int maximumLength, string field)
        => EngagementFields.RequiredText(value, maximumLength, field);

    public static string? OptionalText(string? value, int maximumLength, string field)
        => EngagementFields.OptionalText(value, maximumLength, field);

    public static Guid NonEmptyId(Guid value, string field) => EngagementFields.NonEmptyId(value, field);

    public static Guid? OptionalId(Guid? value, string field)
        => value.HasValue ? NonEmptyId(value.Value, field) : null;

    public static string Email(string? value) => EngagementFields.Email(value);

    public static string Role(string? value, bool guest = false)
    {
        if (value is null || !ProjectRoles.All.Contains(value) || (guest && value == ProjectRoles.ProjectAdministrator))
        {
            throw new BadRequestException("Project.InvalidRole", "A permitted project role is required.");
        }

        return value;
    }
}
