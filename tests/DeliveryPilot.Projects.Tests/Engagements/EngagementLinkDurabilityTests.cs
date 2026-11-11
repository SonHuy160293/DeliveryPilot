using DeliveryPilot.Projects.Application.Abstractions.Authority;
using DeliveryPilot.Projects.Application.Features.Engagements.ClientLinks;
using DeliveryPilot.Projects.Domain.Models.Engagements;
using DeliveryPilot.SharedKernel.Exceptions;
using DeliveryPilot.SharedKernel.Security;

namespace DeliveryPilot.Projects.Tests.Engagements;

public sealed class EngagementLinkDurabilityTests
{
    [Fact]
    public async Task InvalidVerdictIsRecordedBeforeLeaseReleaseEvenAfterCallerDisconnects()
    {
        using var disconnected = new CancellationTokenSource();
        var limiter = new RecordingLimiter();
        var authorization = new ClientLinkAuthorization(new OwnerAuthority(), new Caller());
        var service = new EngagementLinkConfirmationService(authorization, new UnknownCodeStore(disconnected), limiter, TimeProvider.System);
        NotFoundException failure = await Assert.ThrowsAsync<NotFoundException>(() => service.ExecuteAsync(Guid.NewGuid(), "unknown", true, disconnected.Token));
        Assert.Equal("EngagementLink.InvalidCode", failure.Code);
        Assert.True(disconnected.IsCancellationRequested);
        Assert.True(limiter.FailureRecorded);
        Assert.True(limiter.Released);
    }

    private sealed class Caller : ICurrentUserProvider
    {
        public CurrentUser GetCurrentUser() => new(Guid.NewGuid().ToString(), SessionId: Guid.NewGuid().ToString());
    }

    private sealed class OwnerAuthority : IWorkspaceAuthority
    {
        public Task<WorkspaceAuthority> RequireAsync(Guid userId, Guid organizationId, Guid? sessionId, string? requiredPermissionCode, CancellationToken cancellationToken)
            => Task.FromResult(new WorkspaceAuthority(organizationId, Guid.NewGuid(), true, false, new HashSet<string>(), DateTimeOffset.UtcNow));
    }

    private sealed class RecordingLimiter : ILinkAttemptLimiter, IAsyncDisposable
    {
        public int RetryAfterSeconds => 1;
        public bool FailureRecorded { get; private set; }
        public bool Released { get; private set; }
        public Task<IAsyncDisposable> AcquireAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult<IAsyncDisposable>(this);
        public Task<bool> IsLimitedAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task RecordFailureAsync(Guid userId, Guid actingOrganizationId, DateTimeOffset now, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.False(Released);
            FailureRecorded = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Released = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class UnknownCodeStore(CancellationTokenSource disconnected) : IClientLinkStore
    {
        public Task<EngagementLinkCode?> FindCodeByVerifierAsync(byte[] verifier, CancellationToken cancellationToken)
        {
            disconnected.Cancel();
            return Task.FromResult<EngagementLinkCode?>(null);
        }

        public Task<Engagement?> LockEngagementForLinkAsync(Guid organizationId, Guid engagementId, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task<EngagementLinkCode?> ReloadCodeAsync(Guid organizationId, Guid engagementId, Guid codeId, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task<Engagement?> LockLinkedAsync(Guid clientOrganizationId, Guid engagementId, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public Task<IReadOnlyList<LinkedEngagement>> ListAsync(Guid clientOrganizationId, CancellationToken cancellationToken) => throw new InvalidOperationException();
        public void AddEvent(EngagementEvent entry) => throw new InvalidOperationException();
    }
}
