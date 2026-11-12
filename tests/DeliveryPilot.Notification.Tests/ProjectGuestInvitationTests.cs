using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using DeliveryPilot.EntityFrameworkCore.Reliability;
using DeliveryPilot.Messaging.Security;
using DeliveryPilot.Messaging.Configuration;
using DeliveryPilot.Messaging.MassTransit;
using DeliveryPilot.Notification.Application.Abstractions;
using DeliveryPilot.Notification.Application.Features.EmailDelivery.Commands.AcceptProjectGuestInvitationDelivery;
using DeliveryPilot.Notification.Contracts;
using DeliveryPilot.Notification.Domain.Models;
using DeliveryPilot.Notification.Infrastructure.Email;
using DeliveryPilot.Notification.Infrastructure.Email.Providers;
using DeliveryPilot.Notification.Infrastructure.Email.Templates;
using DeliveryPilot.Notification.Infrastructure.Email.Workers;
using DeliveryPilot.Notification.Infrastructure.Operations;
using DeliveryPilot.Notification.Infrastructure.Security;
using DeliveryPilot.SharedKernel.Cqrs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MassTransit;
using Xunit;

namespace DeliveryPilot.Notification.Tests;

public sealed class ProjectGuestInvitationTests
{
    [Fact]
    public void ContractAuthenticatesScopeAndRendersRedactedData()
    {
        using RSA rsa = RSA.Create(3072);
        ProjectGuestInvitationEmailRequestedV1 request = CreateRequest(rsa);
        request.Validate();
        Assert.Throws<ArgumentException>(() => (request with { Producer = "identity" }).Validate());
        Assert.Throws<ArgumentException>(() => (request with { MessageId = Guid.NewGuid() }).Validate());
        Assert.Throws<ArgumentException>(() => (request with { InvitationId = Guid.NewGuid() }).Validate());
        Assert.Throws<ArgumentException>(() => (request with { IssuedAt = request.IssuedAt.ToOffset(TimeSpan.FromHours(1)) }).Validate());
        Assert.Throws<ArgumentException>(() => (request with { ExpiresAt = request.IssuedAt }).Validate());
        var status = new ProjectGuestInvitationEmailStatusV1(Guid.CreateVersion7(), request.MessageId, "notification", 1, DateTimeOffset.UtcNow, "queued", null);
        status.Validate();
        Assert.Throws<ArgumentException>(() => (status with { Producer = "projects" }).Validate());
        Assert.Throws<ArgumentException>(() => (status with { Sequence = 0 }).Validate());
        Assert.Throws<ArgumentException>(() => (status with { FailureCode = "private@example.test" }).Validate());
        Assert.DoesNotContain("private@example.test", new ProjectGuestInvitationEmailData("private@example.test", "Project name", "Viewer", "Inviter", new string('a', 43)).ToString(), StringComparison.Ordinal);
        var cipher = new CipherEnvelope(request.Payload.WrappedKey, request.Payload.Nonce, request.Payload.Tag, request.Payload.Ciphertext);
        Assert.ThrowsAny<CryptographicException>(() => EnvelopeEncryption.Decrypt(cipher, (request with { ProjectId = Guid.CreateVersion7() }).AssociatedData(), rsa.ExportPkcs8PrivateKeyPem()));
        Assert.ThrowsAny<CryptographicException>(() => EnvelopeEncryption.Decrypt(cipher, (request with { InvitationId = Guid.CreateVersion7() }).AssociatedData(), rsa.ExportPkcs8PrivateKeyPem()));
        byte[] plain = EnvelopeEncryption.Decrypt(cipher, request.AssociatedData(), rsa.ExportPkcs8PrivateKeyPem());
        try
        {
            RenderedEmail rendered = ProjectGuestInvitationTemplate.Render(request, plain, new EmailDeliveryOptions { FrontendOrigin = "https://app.example.test", Sender = "sender@example.test" });
            Assert.Contains("#projectId=" + request.ProjectId.ToString("D") + "&invitationId=" + request.InvitationId.ToString("D") + "&proof=", rendered.Text, StringComparison.Ordinal);
            Assert.Contains("Project name", rendered.Text, StringComparison.Ordinal);
            Assert.Contains("Viewer", rendered.Text, StringComparison.Ordinal);
            Assert.Contains("Inviter", rendered.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("Workspace", rendered.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("private@example.test", request.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(new string('a', 43), request.ToString(), StringComparison.Ordinal);
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    [Fact]
    public async Task EligibilityRequiresAValidAuthoritativeAnswer()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".key");
        await File.WriteAllTextAsync(path, "project-test-dispatch-key");
        try
        {
            var handler = new StubHandler();
            using var http = new HttpClient(handler);
            IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ProjectGuestInvitationDispatch:ProjectsOrigin"] = "http://127.0.0.1:9000/", ["ProjectGuestInvitationDispatch:ApiKeyFile"] = path }).Build();
            var client = new ProjectGuestInvitationDispatchEligibility(http, config);
            Guid id = Guid.CreateVersion7();
            handler.Body = "{\"data\":{\"allowed\":true,\"expiresAt\":\"2099-01-01T00:00:00+00:00\"}}";
            Assert.True(await client.IsAllowedAsync(id, CancellationToken.None));
            Assert.Contains("project-guest-invitation-deliveries/" + id.ToString("D"), handler.Path, StringComparison.Ordinal);
            Assert.Equal("project-test-dispatch-key", handler.Key);
            handler.Body = "{\"data\":{\"allowed\":false,\"expiresAt\":null}}";
            Assert.False(await client.IsAllowedAsync(id, CancellationToken.None));
            foreach (string invalid in new[] { "{}", "{\"data\":{}}", "{\"data\":{\"allowed\":false}}", "{\"data\":{\"allowed\":true,\"expiresAt\":null}}", "not json" })
            {
                handler.Body = invalid;
                Assert.Null(await client.IsAllowedAsync(id, CancellationToken.None));
            }
            handler.Status = HttpStatusCode.InternalServerError;
            Assert.Null(await client.IsAllowedAsync(id, CancellationToken.None));
            handler.TimedOut = true;
            Assert.Null(await client.IsAllowedAsync(id, CancellationToken.None));
            IConfiguration missingKey = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ProjectGuestInvitationDispatch:ProjectsOrigin"] = "http://127.0.0.1/", ["ProjectGuestInvitationDispatch:ApiKeyFile"] = path + ".missing" }).Build();
            Assert.Null(await new ProjectGuestInvitationDispatchEligibility(http, missingKey).IsAllowedAsync(id, CancellationToken.None));
            IConfiguration badOrigin = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ProjectGuestInvitationDispatch:ProjectsOrigin"] = "http://example.test/path", ["ProjectGuestInvitationDispatch:ApiKeyFile"] = path }).Build();
            Assert.Null(await new ProjectGuestInvitationDispatchEligibility(http, badOrigin).IsAllowedAsync(id, CancellationToken.None));
        }
        finally { File.Delete(path); }
    }

    [DeliveryDatabaseFact]
    public async Task IntakeIsIdempotentAndEligibilityControlsDeliveryAndStatus()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        var eligibility = new FakeEligibility();
        var provider = new FakeProvider();
        var logs = new CapturedLogs();
        await using var services = fixture.Services(s => { s.AddSingleton<IProjectGuestInvitationDispatchEligibility>(eligibility); s.AddSingleton<IEmailProvider>(provider); s.AddLogging(builder => builder.AddProvider(logs)); });
        ProjectGuestInvitationEmailRequestedV1 request = CreateRequest(fixture.Key);
        for (int i = 0; i < 2; i++)
        {
            await using var scope = services.CreateAsyncScope();
            Assert.True((await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new AcceptProjectGuestInvitationDeliveryCommand(request), CancellationToken.None)).IsSuccess);
        }
        await using var db = fixture.Notification();
        Assert.Equal("project-guest-invitation", (await db.EmailDeliveries.SingleAsync()).RequestKind);
        Assert.Single(await db.Set<OutboxMessage>().Where(x => x.MessageType == ProjectGuestInvitationEmailStatusV1.EventType).ToListAsync());
        var worker = new EmailDeliveryWorker(services.GetRequiredService<IServiceScopeFactory>(), services.GetRequiredService<ILogger<EmailDeliveryWorker>>());
        await worker.RunOnceAsync(CancellationToken.None);
        Assert.Equal(0, provider.Calls);
        db.ChangeTracker.Clear();
        Assert.Equal("AuthorityUnavailable", (await db.EmailDeliveries.SingleAsync()).FailureCode);
        await db.Database.ExecuteSqlRawAsync("UPDATE notification.\"EmailDeliveries\" SET \"NextAttemptAt\" = now(); UPDATE notification.\"EmailDeliveryControls\" SET \"NextRequestAt\" = now()");
        eligibility.Allowed = true;
        await worker.RunOnceAsync(CancellationToken.None);
        Assert.Equal(1, provider.Calls);
        db.ChangeTracker.Clear();
        Assert.Equal("ProviderAccepted", (await db.EmailDeliveries.SingleAsync()).Status);
        List<OutboxMessage> statuses = await db.Set<OutboxMessage>().Where(x => x.MessageType == ProjectGuestInvitationEmailStatusV1.EventType).OrderBy(x => x.AggregateRevision).ToListAsync();
        Assert.Equal(new long[] { 1, 2, 3 }, statuses.Select(x => x.AggregateRevision));
        Assert.All(statuses, status => { Assert.DoesNotContain("private@example.test", status.PayloadJson, StringComparison.Ordinal); Assert.DoesNotContain(new string('a', 43), status.PayloadJson, StringComparison.Ordinal); });
        string captured = string.Join("\n", logs.Messages);
        Assert.DoesNotContain("private@example.test", captured, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('a', 43), captured, StringComparison.Ordinal);
        Assert.DoesNotContain("Project name", captured, StringComparison.Ordinal);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [DeliveryDatabaseFact]
    public async Task MigrationWidensConstraintAndOperationsPublishProjectStatus()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        await using var db = fixture.Notification();
        IMigrator migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261104074835_AddInvitationDeliveryKind");
        await db.Database.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());
        await using var services = fixture.Services();
        ProjectGuestInvitationEmailRequestedV1 request = CreateRequest(fixture.Key);
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new AcceptProjectGuestInvitationDeliveryCommand(request), CancellationToken.None);
        }

        var job = await db.EmailDeliveries.SingleAsync();
        job.DeadLetter("ProviderRejected");
        await db.SaveChangesAsync();
        await using (var scope = services.CreateAsyncScope())
        {
            await NotificationOperations.ExecuteAsync(scope.ServiceProvider, ["redrive", "operator", "test reason", request.MessageId.ToString()], CancellationToken.None);
        }

        db.ChangeTracker.Clear();
        job = await db.EmailDeliveries.SingleAsync();
        Assert.Equal("Pending", job.Status);
        job.Claim(DateTimeOffset.UtcNow);
        job.BeginAttempt(DateTimeOffset.UtcNow);
        job.DeadLetter("ProviderRejected");
        await db.SaveChangesAsync();
        await using (var scope = services.CreateAsyncScope())
        {
            await NotificationOperations.ExecuteAsync(scope.ServiceProvider, ["reconcile-accepted", "operator", "test reason", request.MessageId.ToString(), "provider-test"], CancellationToken.None);
        }

        db.ChangeTracker.Clear();
        Assert.Equal("ProviderAccepted", (await db.EmailDeliveries.SingleAsync()).Status);
        List<OutboxMessage> statuses = await db.Set<OutboxMessage>().Where(x => x.MessageType == ProjectGuestInvitationEmailStatusV1.EventType).OrderBy(x => x.AggregateRevision).ToListAsync();
        Assert.Equal(new[] { "queued", "retrying", "provider-accepted" }, statuses.Select(x => JsonSerializer.Deserialize<ProjectGuestInvitationEmailStatusV1>(x.PayloadJson)!.Status));
        Assert.Equal(new long[] { 1, 2, 3 }, statuses.Select(x => x.AggregateRevision));
    }

    [DeliveryDatabaseFact]
    public async Task ObsoleteTamperedSuppressedExpiredAndProviderFailureStageSafeStatuses()
    {
        foreach (string scenario in new[] { "obsolete", "tampered", "suppressed", "expired", "failure", "retry" })
        {
            await using var fixture = await DeliveryFixture.CreateAsync();
            var eligibility = new FakeEligibility { Allowed = scenario != "obsolete" };
            var provider = new FakeProvider { Result = scenario == "failure" ? new ProviderResult("Permanent", Code: "ProviderRejected") : scenario == "retry" ? new ProviderResult("Retry", Code: "ProviderNetwork") : new ProviderResult("Accepted", "provider-test") };
            await using var services = fixture.Services(s => { s.AddSingleton<IProjectGuestInvitationDispatchEligibility>(eligibility); s.AddSingleton<IEmailProvider>(provider); });
            ProjectGuestInvitationEmailRequestedV1 request = CreateRequest(fixture.Key);
            if (scenario == "tampered") { request = request with { ProjectId = Guid.CreateVersion7() }; }
            if (scenario == "expired") { request = request with { IssuedAt = DateTimeOffset.UtcNow.AddDays(-8), ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1) }; }
            if (scenario == "suppressed")
            {
                await using var seed = fixture.Notification();
                seed.EmailSuppressions.Add(new EmailSuppression { RecipientHash = services.GetRequiredService<EmailPayloadProtection>().RecipientHash("private@example.test"), Reason = "Bounced", CreatedAt = DateTimeOffset.UtcNow });
                await seed.SaveChangesAsync();
            }

            await using (var scope = services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new AcceptProjectGuestInvitationDeliveryCommand(request), CancellationToken.None);
            }

            await new EmailDeliveryWorker(services.GetRequiredService<IServiceScopeFactory>(), NullLogger<EmailDeliveryWorker>.Instance).RunOnceAsync(CancellationToken.None);
            await using var db = fixture.Notification();
            EmailDelivery job = await db.EmailDeliveries.SingleAsync();
            Assert.Equal(scenario switch { "obsolete" or "expired" => "Expired", "tampered" or "failure" => "DeadLetter", "suppressed" => "Suppressed", _ => "Pending" }, job.Status);
            Assert.Equal(scenario is "failure" or "retry" ? 1 : 0, provider.Calls);
            List<OutboxMessage> statuses = await db.Set<OutboxMessage>().Where(x => x.MessageType == ProjectGuestInvitationEmailStatusV1.EventType).OrderBy(x => x.AggregateRevision).ToListAsync();
            Assert.Equal(scenario == "expired" ? 1 : 2, statuses.Count);
            ProjectGuestInvitationEmailStatusV1 last = JsonSerializer.Deserialize<ProjectGuestInvitationEmailStatusV1>(statuses[^1].PayloadJson)!;
            Assert.Equal(scenario switch { "obsolete" => "obsolete", "tampered" or "failure" => "failed", "suppressed" => "suppressed", "expired" => "expired", _ => "retrying" }, last.Status);
            Assert.Equal(statuses.Count, last.Sequence);
            Assert.All(statuses, status => { Assert.DoesNotContain("private@example.test", status.PayloadJson, StringComparison.Ordinal); Assert.DoesNotContain(new string('a', 43), status.PayloadJson, StringComparison.Ordinal); });
        }
    }

    [DeliveryMailpitFact]
    public async Task ProjectInvitationIsDeliveredThroughMailpit()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        await using var services = fixture.Services(s =>
        {
            s.Configure<EmailDeliveryOptions>(options => { options.MailpitHost = "127.0.0.1"; options.MailpitPort = int.Parse(Environment.GetEnvironmentVariable("DELIVERYPILOT_DELIVERY_TEST_SMTP_PORT")!); });
            s.AddSingleton<IProjectGuestInvitationDispatchEligibility>(new FakeEligibility { Allowed = true });
        });
        ProjectGuestInvitationEmailRequestedV1 request = CreateRequest(fixture.Key);
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new AcceptProjectGuestInvitationDeliveryCommand(request), CancellationToken.None);
        }

        await new EmailDeliveryWorker(services.GetRequiredService<IServiceScopeFactory>(), NullLogger<EmailDeliveryWorker>.Instance).RunOnceAsync(CancellationToken.None);
        await using var db = fixture.Notification();
        Assert.Equal("ProviderAccepted", (await db.EmailDeliveries.SingleAsync()).Status);
        using var client = new HttpClient { BaseAddress = new Uri(Environment.GetEnvironmentVariable("DELIVERYPILOT_DELIVERY_TEST_MAILPIT_URL")!) };
        using JsonDocument list = JsonDocument.Parse(await client.GetStringAsync("api/v1/messages"));
        JsonElement message = list.RootElement.GetProperty("messages").EnumerateArray().First(x => x.GetProperty("Subject").GetString() == "Your project invitation");
        using JsonDocument content = JsonDocument.Parse(await client.GetStringAsync("api/v1/message/" + message.GetProperty("ID").GetString()));
        string text = content.RootElement.GetProperty("Text").GetString()!;
        Assert.Contains("Project name", text, StringComparison.Ordinal);
        Assert.Contains("Viewer", text, StringComparison.Ordinal);
        Assert.Contains("Inviter", text, StringComparison.Ordinal);
        Assert.Contains("/join-project#projectId=" + request.ProjectId.ToString("D") + "&invitationId=" + request.InvitationId.ToString("D") + "&proof=", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Workspace", text, StringComparison.Ordinal);
    }

    [DeliveryBrokerFact]
    public async Task RequestAndStatusRouteThroughProjectInvitationEntities()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        string secret = Path.Combine(fixture.Keys, "broker.secret");
        await File.WriteAllTextAsync(secret, Environment.GetEnvironmentVariable("DELIVERYPILOT_DELIVERY_TEST_RABBIT_PASSWORD"));
        var options = new MessagingOptions { Host = "localhost", Port = ushort.Parse(Environment.GetEnvironmentVariable("DELIVERYPILOT_DELIVERY_TEST_RABBIT_PORT")!), VirtualHost = "/", Username = "deliverypilot_test", PasswordFile = secret, Development = true, QuorumSize = 1 };
        var requestReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var statusReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string queue = "deliverypilot.test." + Guid.NewGuid().ToString("N");
        var bus = Bus.Factory.CreateUsingRabbitMq(config =>
        {
            RabbitConfiguration.ConfigureHost(config, options);
            config.Message<ProjectGuestInvitationEmailRequestedV1>(m => m.SetEntityName("deliverypilot.notification.project-guest-invitation-email-requested.v1"));
            config.Message<ProjectGuestInvitationEmailStatusV1>(m => m.SetEntityName("deliverypilot.notification.project-guest-invitation-email-status.v1"));
            config.ReceiveEndpoint(queue, endpoint =>
            {
                RabbitConfiguration.ConfigureQueue(endpoint, options);
                endpoint.Handler<ProjectGuestInvitationEmailRequestedV1>(context => { requestReceived.TrySetResult(); return Task.CompletedTask; });
                endpoint.Handler<ProjectGuestInvitationEmailStatusV1>(context => { statusReceived.TrySetResult(); return Task.CompletedTask; });
            });
        });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await bus.StartAsync(deadline.Token);
        try
        {
            ProjectGuestInvitationEmailRequestedV1 request = CreateRequest(fixture.Key);
            await bus.Publish(request, deadline.Token);
            await bus.Publish(new ProjectGuestInvitationEmailStatusV1(Guid.CreateVersion7(), request.MessageId, "notification", 1, DateTimeOffset.UtcNow, "queued", null), deadline.Token);
            await Task.WhenAll(requestReceived.Task, statusReceived.Task).WaitAsync(deadline.Token);
        }
        finally { await bus.StopAsync(CancellationToken.None); }
    }

    private static ProjectGuestInvitationEmailRequestedV1 CreateRequest(RSA rsa)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var request = new ProjectGuestInvitationEmailRequestedV1(Guid.CreateVersion7(), Guid.CreateVersion7(), null, "projects", Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), now, now.AddDays(7), new EncryptedEmailPayload(1, "v1", "x", "x", "x", "x"));
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(new ProjectGuestInvitationEmailData("private@example.test", "Project name", "Viewer", "Inviter", new string('a', 43)));
        try
        {
            CipherEnvelope cipher = EnvelopeEncryption.Encrypt(plain, request.AssociatedData(), rsa.ExportSubjectPublicKeyInfoPem());
            return request with { Payload = new EncryptedEmailPayload(1, "v1", cipher.WrappedKey, cipher.Nonce, cipher.Tag, cipher.Ciphertext) };
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    private sealed class FakeEligibility : IProjectGuestInvitationDispatchEligibility
    {
        public bool? Allowed { get; set; }
        public Task<bool?> IsAllowedAsync(Guid deliveryId, CancellationToken cancellationToken) => Task.FromResult(Allowed);
    }

    private sealed class FakeProvider : IEmailProvider
    {
        public int Calls { get; private set; }
        public ProviderResult Result { get; set; } = new("Accepted", "provider-test");
        public Task<ProviderResult> SendAsync(Guid id, string frozenJson, CancellationToken cancellationToken) { Calls++; return Task.FromResult(Result); }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public string Body { get; set; } = "{}";
        public string Path { get; private set; } = "";
        public string Key { get; private set; } = "";
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public bool TimedOut { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (TimedOut) { throw new TaskCanceledException(); }
            Path = request.RequestUri!.AbsolutePath;
            Key = request.Headers.GetValues("X-Invitation-Dispatch-Key").Single();
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Body) });
        }
    }

    private sealed class CapturedLogs : ILoggerProvider
    {
        public System.Collections.Concurrent.ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Logger(Messages);
        public void Dispose() { }

        private sealed class Logger(System.Collections.Concurrent.ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, exception));
        }
    }
}
