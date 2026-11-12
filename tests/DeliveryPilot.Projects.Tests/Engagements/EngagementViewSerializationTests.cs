using System.Text.Json;
using DeliveryPilot.Projects.Application.Features.Engagements.Queries;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.Tests.Engagements;

public sealed class EngagementViewSerializationTests
{
    [Fact]
    public void SummaryHasExactlyTheAllowlistedFieldsThroughTheObjectEnvelopeAndList()
    {
        var summary = new EngagementSummary(Guid.NewGuid(), "Client", "Engagement", "Ref", "Active", [], 1);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        JsonElement envelope = JsonSerializer.SerializeToElement(ApiResponse.Ok(summary), options);
        AssertSummary(envelope.GetProperty("data"));
        var page = new EngagementPage<EngagementListEntry>([summary], null);
        JsonElement list = JsonSerializer.SerializeToElement(page, options);
        AssertSummary(list.GetProperty("items")[0]);
    }

    private static void AssertSummary(JsonElement summary)
    {
        Assert.Equal("summary", summary.GetProperty("tier").GetString());
        Assert.Equal(["agreementReference", "clientDisplayName", "engagementName", "id", "milestones", "status", "tier", "version"], summary.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
    }
}
