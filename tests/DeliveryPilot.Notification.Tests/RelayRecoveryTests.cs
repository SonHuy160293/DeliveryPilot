using System.Collections.Concurrent;
using DeliveryPilot.EntityFrameworkCore.Reliability;
using DeliveryPilot.Identity.Infrastructure.Messaging;
using DeliveryPilot.Identity.Infrastructure.Persistence.Core;
using DeliveryPilot.Identity.Infrastructure.Persistence.Outbox;
using DeliveryPilot.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeliveryPilot.Notification.Tests;

public sealed class RelayRecoveryTests
{
    [DeliveryDatabaseFact]
    public async Task TwoRelaysRecoverPublishedBeforeMarkWithStablePayload()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        var request = PersistenceIntegrationTests.Request(fixture);
        await using (var seed = fixture.Identity())
        {
            await new IdentityUnitOfWork(seed).ExecuteInTransactionAsync(async ct =>
            {
                await new IdentityEmailOutbox(seed).StageAsync(request, ct);
                return true;
            });
        }

        var publisher = new CapturingPublisher();
        await using (var firstContext = fixture.Identity())
        {
            var first = Assert.Single(await new IdentityOutboxDispatchStore(firstContext).ClaimAsync(default));
            await publisher.PublishAsync(first, default); // Simulated broker confirmation before process failure.
        }

        await using (var update = fixture.Identity())
        {
            await update.Database.ExecuteSqlInterpolatedAsync($"UPDATE reliability.\"OutboxMessages\" SET \"LeaseUntil\" = {DateTimeOffset.UtcNow.AddMinutes(-1)} WHERE \"MessageId\" = {request.MessageId}");
        }

        var services = new ServiceCollection();
        services.AddScoped(_ => fixture.Identity());
        services.AddScoped<IOutboxDispatchStore, IdentityOutboxDispatchStore>();
        services.AddSingleton<IIntegrationEventPublisher>(publisher);
        await using var provider = services.BuildServiceProvider();
        var a = new OutboxRelay(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<OutboxRelay>.Instance);
        var b = new OutboxRelay(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<OutboxRelay>.Instance);
        await Task.WhenAll(a.RunOnceAsync(default), b.RunOnceAsync(default));
        Assert.Equal(2, publisher.Messages.Count);
        Assert.All(publisher.Messages, value => Assert.Equal((request.MessageId, publisher.Messages.First().Payload), value));
        await using var verify = fixture.Identity();
        Assert.Equal(OutboxStatus.Published, (await verify.Set<OutboxMessage>().SingleAsync()).Status);
    }

    private sealed class CapturingPublisher : IIntegrationEventPublisher
    {
        public ConcurrentQueue<(Guid Id, string Payload)> Messages { get; } = new();

        public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
        {
            Messages.Enqueue((message.MessageId, message.PayloadJson));
            return Task.CompletedTask;
        }
    }

}
