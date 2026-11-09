using System.Security.Cryptography;
using System.Text;
using DeliveryPilot.Projects.Application.Features.Engagements.Mutations;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;

namespace DeliveryPilot.Projects.Application.Features.Engagements.Commands.IssueEngagementLinkCode;

public sealed class IssueEngagementLinkCodeHandler(EngagementMutationService mutations, IEngagementLinkCodeGenerator generator) : ICommandHandler<IssueEngagementLinkCodeCommand, IssuedEngagementLinkCode>
{
    public async Task<Result<IssuedEngagementLinkCode>> HandleAsync(IssueEngagementLinkCodeCommand request, CancellationToken cancellationToken)
    {
        EngagementMutationContext context = await mutations.LockAsync(request.OrganizationId, request.EngagementId, cancellationToken);
        Engagement engagement = context.Engagement;
        engagement.EnsureCanIssueLinkCode();
        engagement.RequireVersion(request.ExpectedVersion!.Value);
        string code = generator.Generate();
        byte[] verifier = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        Guid? invalidated = await mutations.InvalidateOutstandingCodeAsync(context, cancellationToken);
        var issued = EngagementLinkCode.Create(request.OrganizationId, request.EngagementId, context.UserId, context.OccurredAt, verifier);
        mutations.AddLinkCode(issued);
        engagement.RecordChange(context.OccurredAt);
        mutations.Record(context, "link-code.issued", "LinkCode", issued.Id, new { linkCodeId = issued.Id, issued.ExpiresAt, invalidatedLinkCodeId = invalidated }, null);
        return Result.Success(new IssuedEngagementLinkCode(code, issued.ExpiresAt, engagement.Version));
    }
}
