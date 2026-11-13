using DeliveryPilot.Projects.API.Contracts.Projects;
using DeliveryPilot.Projects.Application.Features.Projects.Commands.CreateProjectStakeholder;
using DeliveryPilot.Projects.Application.Features.Projects.Commands.UpdateProjectStakeholder;
using DeliveryPilot.Projects.Domain.Models.Projects;

namespace DeliveryPilot.Projects.Tests.Projects;

public sealed class StakeholderRedactionTests
{
    [Fact]
    public void StakeholderRequestAndCommandsDoNotFormatPersonalData()
    {
        var request = new ProjectStakeholderRequest(1, "Private Name", "Client", null, null, null, "private@example.test", null);
        ProjectStakeholderValues values = request.ToValues();
        var create = new CreateProjectStakeholderCommand(Guid.NewGuid(), Guid.NewGuid(), values);
        var update = new UpdateProjectStakeholderCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, values);
        foreach (string formatted in new[] { request.ToString(), values.ToString(), create.ToString(), update.ToString() })
        {
            Assert.DoesNotContain("Private Name", formatted, StringComparison.Ordinal);
            Assert.DoesNotContain("private@example.test", formatted, StringComparison.Ordinal);
        }
    }
}
