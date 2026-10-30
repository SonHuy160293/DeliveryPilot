using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.Organizations.Commands.CreateOrganization;

public sealed record CreateOrganizationCommand(string? Name, Guid AttemptId, string CorrelationId) : ICommand<CreateOrganizationResult>
{
    public override string ToString() => nameof(CreateOrganizationCommand);
}
