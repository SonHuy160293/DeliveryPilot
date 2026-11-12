using System.Security.Cryptography;
using DeliveryPilot.Projects.Domain.Models.Projects;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Projects.Tests.Domain;

public sealed class ProjectCreationTests
{
    private static readonly DateTimeOffset Instant = new(2026, 11, 12, 14, 0, 0, TimeSpan.FromHours(7));

    [Theory]
    [InlineData("Client")]
    [InlineData("Internal")]
    public void CreationStartsDraftWithAnEmptyIndependentBrief(string kind)
    {
        var organizationId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var project = Project.Create(organizationId, creatorId, Instant, "  Delivery  ", "  Ship safely  ", kind);
        var brief = ProjectBrief.CreateFor(project);

        Assert.Equal(7, project.Id.Version);
        Assert.Equal(organizationId, project.OrganizationId);
        Assert.Equal(creatorId, project.CreatedByUserId);
        Assert.Equal("Delivery", project.Name);
        Assert.Equal("Ship safely", project.Purpose);
        Assert.Equal("Draft", project.Status);
        Assert.Equal(2, project.SprintLengthWeeks);
        Assert.Equal(1, project.Version);
        Assert.Equal(Instant.ToUniversalTime(), project.CreatedAt);
        Assert.Equal(TimeSpan.Zero, project.CreatedAt.Offset);
        Assert.Equal(project.CreatedAt, project.UpdatedAt);
        Assert.Null(project.EngagementId);
        Assert.Null(project.ClientProductOwnerParticipantId);
        Assert.Null(project.ClientPoAssignedAt);
        Assert.Null(project.ClientPoAssignedByUserId);
        Assert.Null(project.ActivatedAt);
        Assert.Null(project.ActivatedByUserId);
        Assert.Null(project.ClientPoWaiverReason);
        Assert.Equal(project.Id, brief.ProjectId);
        Assert.Equal(project.OrganizationId, brief.OrganizationId);
        Assert.Equal(1, brief.Version);
        Assert.All(new[] { brief.Problem, brief.ObjectivesAndSuccessCriteria, brief.ScopeAndExclusions, brief.Assumptions, brief.ConstraintsAndDependencies, brief.OpenQuestions, brief.ProductGoal }, value => Assert.Null(value));
        Assert.Null(brief.UpdatedAt);
        Assert.Null(brief.UpdatedByUserId);
    }

    [Theory]
    [InlineData("", "Purpose", "Client")]
    [InlineData("Name", " ", "Internal")]
    [InlineData("Name", "Purpose", "Unknown")]
    public void InvalidCreationFieldsAreRejected(string name, string purpose, string kind)
        => Assert.Throws<BadRequestException>(() => Project.Create(Guid.NewGuid(), Guid.NewGuid(), Instant, name, purpose, kind));

    [Fact]
    public void CreationEnforcesTextBoundsKindAndScalarIdentifiers()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var engagementId = Guid.CreateVersion7();
        Assert.Throws<BadRequestException>(() => Project.Create(organizationId, userId, Instant, new string('n', 161), "Purpose", "Client"));
        Assert.Throws<BadRequestException>(() => Project.Create(organizationId, userId, Instant, "Name", new string('p', 501), "Client"));
        Assert.Throws<BadRequestException>(() => Project.Create(organizationId, userId, Instant, "Name", "Purpose", "Internal", engagementId));
        Assert.Throws<ArgumentException>(() => Project.Create(Guid.Empty, userId, Instant, "Name", "Purpose", "Client"));
        Assert.Throws<ArgumentException>(() => Project.Create(organizationId, Guid.Empty, Instant, "Name", "Purpose", "Client"));
        Assert.Throws<ArgumentException>(() => Project.Create(organizationId, userId, Instant, "Name", "Purpose", "Client", Guid.Empty));
        Project project = Project.Create(organizationId, userId, Instant, new string('n', 160), new string('p', 500), "Client", engagementId);
        Assert.Equal(engagementId, project.EngagementId);
    }

    [Fact]
    public void MembersGuestsAndRoleGrantsKeepScopeAndGuestRestrictions()
    {
        var project = Project.Create(Guid.NewGuid(), Guid.NewGuid(), Instant, "Name", "Purpose", "Internal");
        var member = ProjectParticipant.CreateMember(project.OrganizationId, project.Id, project.CreatedByUserId, Instant);
        var invitationId = Guid.CreateVersion7();
        var guest = ProjectParticipant.CreateGuest(project.OrganizationId, project.Id, Guid.NewGuid(), Instant, invitationId);
        foreach (ProjectParticipant participant in new[] { member, guest })
        {
            Assert.Equal(7, participant.Id.Version);
            Assert.Equal(project.OrganizationId, participant.OrganizationId);
            Assert.Equal(project.Id, participant.ProjectId);
            Assert.Equal("Active", participant.Status);
            Assert.Equal(Instant.ToUniversalTime(), participant.FirstJoinedAt);
            Assert.Equal(TimeSpan.Zero, participant.ActiveSince.Offset);
            Assert.Equal(1, participant.Version);
            Assert.Null(participant.EndedAt);
            Assert.Null(participant.EndedByUserId);
        }

        Assert.Equal("Member", member.Kind);
        Assert.Null(member.AdmittedViaInvitationId);
        Assert.Equal("Guest", guest.Kind);
        Assert.Equal(invitationId, guest.AdmittedViaInvitationId);
        ProjectParticipantRole role = ProjectParticipantRole.Grant(member, "ProjectAdministrator", project.CreatedByUserId, Instant);
        Assert.Equal(7, role.Id.Version);
        Assert.Equal(project.OrganizationId, role.OrganizationId);
        Assert.Equal(project.Id, role.ProjectId);
        Assert.Equal(member.Id, role.ParticipantId);
        Assert.Equal(Instant.ToUniversalTime(), role.GrantedAt);
        Assert.Null(role.RevokedAt);
        Assert.Null(role.RevokedByUserId);
        Assert.Throws<BadRequestException>(() => ProjectParticipantRole.Grant(guest, "ProjectAdministrator", project.CreatedByUserId, Instant));
        Assert.Throws<BadRequestException>(() => ProjectParticipantRole.Grant(member, "Unknown", project.CreatedByUserId, Instant));
        Assert.Throws<ArgumentException>(() => ProjectParticipant.CreateGuest(project.OrganizationId, project.Id, guest.UserId, Instant, Guid.Empty));
    }

    [Fact]
    public void InvitationsNormalizeEmailProtectProofAndExpireExactlyOneWeekLater()
    {
        byte[] verifier = RandomNumberGenerator.GetBytes(32);
        byte[] original = (byte[])verifier.Clone();
        var invitation = ProjectGuestInvitation.Issue(Guid.NewGuid(), Guid.CreateVersion7(), Guid.NewGuid(), Instant, " GUEST@Example.com ", "Viewer", verifier);
        Array.Fill(verifier, (byte)0);
        byte[] exposed = invitation.TokenVerifier;
        Array.Fill(exposed, (byte)0);

        Assert.Equal(original, invitation.TokenVerifier);
        Assert.Equal(7, invitation.Id.Version);
        Assert.Equal("guest@example.com", invitation.RecipientEmail);
        Assert.Equal("Pending", invitation.Status);
        Assert.Equal(1, invitation.Version);
        Assert.Equal(Instant.ToUniversalTime(), invitation.CreatedAt);
        Assert.Equal(invitation.CreatedAt, invitation.LastIssuedAt);
        Assert.Equal(invitation.LastIssuedAt.AddHours(168), invitation.ExpiresAt);
        Assert.Null(invitation.CurrentDeliveryMessageId);
        Assert.Null(invitation.ClosedAt);
        Assert.Null(invitation.ClosedByUserId);
        Assert.DoesNotContain(Convert.ToHexString(original), invitation.ToString()!, StringComparison.Ordinal);
        Assert.Throws<BadRequestException>(() => ProjectGuestInvitation.Issue(invitation.OrganizationId, invitation.ProjectId, invitation.IssuedByUserId, Instant, "guest@example.com", "ProjectAdministrator", original));
        Assert.Throws<BadRequestException>(() => ProjectGuestInvitation.Issue(invitation.OrganizationId, invitation.ProjectId, invitation.IssuedByUserId, Instant, "invalid", "Viewer", original));
        Assert.Throws<BadRequestException>(() => ProjectGuestInvitation.Issue(invitation.OrganizationId, invitation.ProjectId, invitation.IssuedByUserId, Instant, "guest@example.com", "Viewer", new byte[31]));

        var deliveryId = Guid.CreateVersion7();
        var delivery = ProjectGuestInvitationDelivery.Create(invitation.OrganizationId, invitation.ProjectId, invitation.Id, deliveryId, Instant, invitation.ExpiresAt);
        Assert.Equal(deliveryId, delivery.DeliveryMessageId);
        Assert.Equal("queued", delivery.Status);
        Assert.Equal(0, delivery.LastEventSequence);
        Assert.Equal(1, delivery.Version);
        Assert.Equal(TimeSpan.Zero, delivery.IssuedAt.Offset);
        Assert.Equal(delivery.IssuedAt, delivery.UpdatedAt);
        Assert.Null(delivery.FailureCode);
        Assert.Throws<BadRequestException>(() => ProjectGuestInvitationDelivery.Create(invitation.OrganizationId, invitation.ProjectId, invitation.Id, Guid.NewGuid(), Instant, invitation.ExpiresAt));
        Assert.Throws<BadRequestException>(() => ProjectGuestInvitationDelivery.Create(invitation.OrganizationId, invitation.ProjectId, invitation.Id, deliveryId, Instant, Instant));
    }

    [Fact]
    public void StakeholdersValidateFieldsWithoutNeedingAnAccount()
    {
        var stakeholder = ProjectStakeholder.Create(Guid.NewGuid(), Guid.CreateVersion7(), Guid.NewGuid(), Instant, " Ada ", "Client", " Agency ", " Lead ", " Budget ", " ADA@Example.com ");
        Assert.Equal(7, stakeholder.Id.Version);
        Assert.Equal("Ada", stakeholder.Name);
        Assert.Equal("Agency", stakeholder.OrganizationName);
        Assert.Equal("Lead", stakeholder.Responsibilities);
        Assert.Equal("Budget", stakeholder.DecisionAreas);
        Assert.Equal("ada@example.com", stakeholder.Email);
        Assert.Null(stakeholder.ParticipantId);
        Assert.Null(stakeholder.RetiredAt);
        Assert.Null(stakeholder.RetiredByUserId);
        Assert.Equal(1, stakeholder.Version);
        Assert.Equal(Instant.ToUniversalTime(), stakeholder.CreatedAt);
        Assert.Equal(stakeholder.CreatedAt, stakeholder.UpdatedAt);
        Assert.Throws<BadRequestException>(() => ProjectStakeholder.Create(stakeholder.OrganizationId, stakeholder.ProjectId, stakeholder.CreatedByUserId, Instant, " ", "Client"));
        Assert.Throws<BadRequestException>(() => ProjectStakeholder.Create(stakeholder.OrganizationId, stakeholder.ProjectId, stakeholder.CreatedByUserId, Instant, "Name", "Unknown"));
        Assert.Throws<BadRequestException>(() => ProjectStakeholder.Create(stakeholder.OrganizationId, stakeholder.ProjectId, stakeholder.CreatedByUserId, Instant, new string('n', 201), "Vendor"));
        Assert.Throws<BadRequestException>(() => ProjectStakeholder.Create(stakeholder.OrganizationId, stakeholder.ProjectId, stakeholder.CreatedByUserId, Instant, "Name", "Vendor", new string('o', 201)));
        Assert.Throws<BadRequestException>(() => ProjectStakeholder.Create(stakeholder.OrganizationId, stakeholder.ProjectId, stakeholder.CreatedByUserId, Instant, "Name", "Vendor", responsibilities: new string('r', 2001)));
        Assert.Throws<BadRequestException>(() => ProjectStakeholder.Create(stakeholder.OrganizationId, stakeholder.ProjectId, stakeholder.CreatedByUserId, Instant, "Name", "Vendor", decisionAreas: new string('d', 2001)));
    }

    [Theory]
    [InlineData("Project")]
    [InlineData("Brief")]
    [InlineData("Participant")]
    [InlineData("RoleGrant")]
    [InlineData("Invitation")]
    [InlineData("Stakeholder")]
    [InlineData("ClientPo")]
    [InlineData("GuestShare")]
    public void EventsAcceptOnlyObjectsAndSupportedSubjects(string subject)
    {
        var organizationId = Guid.NewGuid();
        var projectId = Guid.CreateVersion7();
        var userId = Guid.NewGuid();
        var entry = ProjectEvent.Create(organizationId, projectId, " project.created ", subject, projectId, userId, organizationId, Instant, 1, "{}", " reason ");
        Assert.Equal(7, entry.Id.Version);
        Assert.Equal("project.created", entry.EventType);
        Assert.Equal("reason", entry.Reason);
        Assert.Equal(TimeSpan.Zero, entry.OccurredAt.Offset);
        Assert.Throws<ArgumentException>(() => ProjectEvent.Create(organizationId, projectId, "project.created", subject, projectId, userId, organizationId, Instant, 1, "[]"));
        Assert.Throws<ArgumentException>(() => ProjectEvent.Create(organizationId, projectId, "project.created", "Unknown", projectId, userId, organizationId, Instant, 1, "{}"));
        Assert.Throws<ArgumentOutOfRangeException>(() => ProjectEvent.Create(organizationId, projectId, "project.created", subject, projectId, userId, organizationId, Instant, 0, "{}"));
    }

    [Theory]
    [InlineData("project.read", "BusinessAnalyst,DevOps,Developer,ProjectAdministrator,ProjectManager,QualityAssurance,Viewer")]
    [InlineData("project.brief.edit", "BusinessAnalyst,ProjectAdministrator,ProjectManager")]
    [InlineData("project.stakeholders.edit", "ProjectAdministrator,ProjectManager")]
    [InlineData("project.cadence.edit", "ProjectAdministrator,ProjectManager")]
    [InlineData("project.participants.manage", "ProjectAdministrator,ProjectManager")]
    [InlineData("project.guests.manage", "ProjectAdministrator,ProjectManager")]
    [InlineData("project.client-po.assign", "ProjectAdministrator")]
    [InlineData("project.initialize", "ProjectAdministrator")]
    [InlineData("project.administrators.manage", "ProjectAdministrator")]
    [InlineData("project.history.read", "ProjectAdministrator,ProjectManager")]
    public void MatrixMatchesEveryPermissionInFr004005(string permission, string expectedRoles)
        => Assert.Equal(expectedRoles.Split(','), ProjectPermissions.ByRole.Where(pair => pair.Value.Contains(permission)).Select(pair => pair.Key).Order(StringComparer.Ordinal));

    [Fact]
    public void CatalogAndGuestExclusionsHaveNoExtraRolesOrPermissions()
    {
        string[] roles = ["BusinessAnalyst", "DevOps", "Developer", "ProjectAdministrator", "ProjectManager", "QualityAssurance", "Viewer"];
        Assert.Equal(roles, ProjectRoles.All.Order(StringComparer.Ordinal));
        Assert.Equal(roles, ProjectPermissions.ByRole.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(10, ProjectPermissions.ByRole.Values.SelectMany(permissions => permissions).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(["project.cadence.edit", "project.guests.manage", "project.history.read", "project.participants.manage"], ProjectPermissions.MemberOnly.Order(StringComparer.Ordinal));
    }
}
