namespace DeliveryPilot.Projects.Domain.Models.Projects;

public sealed record ProjectCreation(Project Project, ProjectBrief Brief, ProjectParticipant Participant, ProjectParticipantRole AdministratorRole);
