using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Domain.Models.Projects;

public sealed record ProjectStakeholderValues(string? Name, string? Side, string? OrganizationName, string? Responsibilities, string? DecisionAreas, string? Email, Guid? ParticipantId)
{
    public ProjectStakeholderValues Normalize()
    {
        if (Side is not (ProjectStakeholder.VendorSide or ProjectStakeholder.ClientSide))
        {
            throw new BadRequestException("Project.InvalidStakeholderSide", "A vendor or client side is required.");
        }

        return new ProjectStakeholderValues(
            ProjectFields.RequiredText(Name, 200, nameof(Name)), Side,
            ProjectFields.OptionalText(OrganizationName, 200, nameof(OrganizationName)),
            ProjectFields.OptionalText(Responsibilities, 2000, nameof(Responsibilities)),
            ProjectFields.OptionalText(DecisionAreas, 2000, nameof(DecisionAreas)),
            string.IsNullOrWhiteSpace(Email) ? null : ProjectFields.Email(Email), ParticipantId);
    }

    public override string ToString() => nameof(ProjectStakeholderValues);
}
