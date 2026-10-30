using System.Security.Cryptography;
using System.Text;
using DeliveryPilot.Messaging.Security;
using DeliveryPilot.Identity.Infrastructure.Messaging;
using DeliveryPilot.Notification.Contracts;
using DeliveryPilot.Notification.Infrastructure.Email;
using DeliveryPilot.Notification.Infrastructure.Email.Templates;
using DeliveryPilot.Notification.Application.Features.EmailDelivery.Commands.AcceptInvitationDelivery;
using DeliveryPilot.SharedKernel.Cqrs;
using DeliveryPilot.EntityFrameworkCore.Reliability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using DeliveryPilot.Notification.Application.Abstractions;
using DeliveryPilot.Notification.Infrastructure.Email.Providers;
using DeliveryPilot.Notification.Infrastructure.Email.Workers;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.Identity.Infrastructure.Persistence.Core;
using DeliveryPilot.Identity.Infrastructure.Persistence.Repositories;
using DeliveryPilot.Identity.Application.Abstractions.Invitations;
using Microsoft.Extensions.Options;
using DeliveryPilot.SharedKernel.Exceptions;
using Xunit;
using System.Text.Json;

namespace DeliveryPilot.Notification.Tests;

public sealed class InvitationDeliveryContractTests
{
    [DeliveryDatabaseFact]
    public async Task DisabledDeliveryKeepsInvitationQueuedWithoutProviderCall()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        var provider = new FakeProvider();
        await using var services = fixture.Services(s =>
        {
            s.Configure<EmailDeliveryOptions>(options => options.Mode = "Disabled");
            s.AddSingleton<IInvitationDispatchEligibility>(new FakeEligibility { Allowed = true });
            s.AddSingleton<IEmailProvider>(provider);
        });
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var request = InvitationEmailEnvelopeFactory.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), now, now.AddDays(7), new OrganizationInvitationEmailData("recipient@example.test", "Workspace", ["Member"], new string('a', 43)), "v1", fixture.Key.ExportSubjectPublicKeyInfoPem());
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new AcceptInvitationDeliveryCommand(request), CancellationToken.None);
        }

        await new EmailDeliveryWorker(services.GetRequiredService<IServiceScopeFactory>(), NullLogger<EmailDeliveryWorker>.Instance).RunOnceAsync(CancellationToken.None);
        Assert.Equal(0, provider.Calls);
        await using var db = fixture.Notification();
        Assert.Equal("Pending", (await db.EmailDeliveries.SingleAsync()).Status);
        Assert.Single(await db.Set<OutboxMessage>().Where(x => x.MessageType == OrganizationInvitationEmailStatusV1.EventType).ToListAsync());
    }

    [DeliveryDatabaseFact]
    public async Task NotificationMigrationPreservesExistingVerificationJobsAcrossRollbackAndUpgrade()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        await using var db = fixture.Notification();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var job = DeliveryPilot.Notification.Domain.Models.EmailDelivery.Create(Guid.CreateVersion7(), new string('A', 64), "{}", now, now.AddDays(1));
        db.EmailDeliveries.Add(job);
        await db.SaveChangesAsync();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261025182023_InitialNotificationDelivery");
        Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM notification.\"EmailDeliveries\"").SingleAsync());
        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        var stored = await db.EmailDeliveries.SingleAsync();
        Assert.Equal(job.Id, stored.Id);
        Assert.Equal("verification", stored.RequestKind);
        Assert.Equal(0, stored.InvitationStatusSequence);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [DeliveryDatabaseFact]
    public async Task IdentityStagerWritesOnlyEncryptedOutboxInTheInvitationTransaction()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        await using var db = fixture.Identity();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var user = User.Create("stager-owner@example.test", "Owner", status: "active");
        var organization = Organization.Create("Private workspace", user.Id, now);
        var role = OrganizationRole.CreateCustom(organization.Id, "Offered name", user.Id, now);
        Guid invitationId = Guid.CreateVersion7();
        Guid deliveryId = Guid.CreateVersion7();
        var invitation = OrganizationInvitation.CreateWithId(invitationId, organization.Id, "recipient@example.test", user.Id, now, now.AddDays(7), new byte[32], deliveryId);
        using var codes = JsonDocument.Parse("[]");
        var offer = OrganizationInvitationRoleOffer.Create(organization.Id, invitation.Id, role.Id, role.Name, role.PermissionSetVersion, codes.RootElement);
        var delivery = OrganizationInvitationDelivery.Create(organization.Id, invitation.Id, deliveryId, now, now.AddDays(7));
        db.Users.Add(user);
        db.Organizations.Add(organization);
        db.OrganizationRoles.Add(role);
        await db.SaveChangesAsync();
        var unconfigured = new InvitationDeliveryStager(db, Options.Create(new InvitationDeliveryProducerOptions()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new IdentityUnitOfWork(db).ExecuteInTransactionAsync(async ct =>
        {
            db.OrganizationInvitations.Add(invitation);
            db.OrganizationInvitationRoleOffers.Add(offer);
            db.OrganizationInvitationDeliveries.Add(delivery);
            await unconfigured.StageAsync(invitation, [offer], delivery, new InvitationProof(new string('a', 43), new byte[32]), "test", ct);
            return true;
        }));
        Assert.Empty(await db.OrganizationInvitations.ToListAsync());
        var stager = new InvitationDeliveryStager(db, Options.Create(new InvitationDeliveryProducerOptions { KeyId = "v1", PublicKeyFile = Path.Combine(fixture.Keys, "v1.public.pem") }));
        var proof = new InvitationProof(new string('a', 43), new byte[32]);
        await new IdentityUnitOfWork(db).ExecuteInTransactionAsync(async ct =>
        {
            db.OrganizationInvitations.Add(invitation);
            db.OrganizationInvitationRoleOffers.Add(offer);
            db.OrganizationInvitationDeliveries.Add(delivery);
            await stager.StageAsync(invitation, [offer], delivery, proof, Guid.CreateVersion7().ToString(), ct);
            return true;
        });
        OutboxMessage row = await db.Set<OutboxMessage>().SingleAsync();
        Assert.Equal(OrganizationInvitationEmailRequestedV1.EventType, row.MessageType);
        Assert.DoesNotContain(invitation.RecipientEmail, row.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain(proof.Value, row.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain(role.Name, row.PayloadJson, StringComparison.Ordinal);
        var request = JsonSerializer.Deserialize<OrganizationInvitationEmailRequestedV1>(row.PayloadJson)!;
        byte[] plain = EnvelopeEncryption.Decrypt(new(request.Payload.WrappedKey, request.Payload.Nonce, request.Payload.Tag, request.Payload.Ciphertext), request.AssociatedData(), fixture.Key.ExportPkcs8PrivateKeyPem());
        try
        {
            var data = JsonSerializer.Deserialize<OrganizationInvitationEmailData>(plain)!;
            Assert.Equal("Offered name", Assert.Single(data.OfferedRoleNames));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    [DeliveryDatabaseFact]
    public async Task ObsoleteGenerationNeverCallsProvider()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        var provider = new FakeProvider();
        await using var services = fixture.Services(s =>
        {
            s.AddSingleton<IInvitationDispatchEligibility>(new FakeEligibility { Allowed = false });
            s.AddSingleton<IEmailProvider>(provider);
        });
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var request = InvitationEmailEnvelopeFactory.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), now, now.AddDays(7), new OrganizationInvitationEmailData("recipient@example.test", "Workspace", ["Member"], new string('a', 43)), "v1", fixture.Key.ExportSubjectPublicKeyInfoPem());
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new AcceptInvitationDeliveryCommand(request), CancellationToken.None);
        }

        await new EmailDeliveryWorker(services.GetRequiredService<IServiceScopeFactory>(), NullLogger<EmailDeliveryWorker>.Instance).RunOnceAsync(CancellationToken.None);
        Assert.Equal(0, provider.Calls);
        await using var db = fixture.Notification();
        var job = await db.EmailDeliveries.SingleAsync();
        Assert.Equal("Expired", job.Status);
        Assert.Equal("Obsolete", job.FailureCode);
        Assert.Equal("{}", job.RequestJson);
    }

    [DeliveryDatabaseFact]
    public async Task StatusProjectionRejectsConflictingDuplicatesAndIgnoresStaleGenerations()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        await using var db = fixture.Identity();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var user = User.Create("projection@example.test", "Owner", status: "active");
        var organization = Organization.Create("Workspace", user.Id, now);
        Guid invitationId = Guid.CreateVersion7();
        Guid deliveryId = Guid.CreateVersion7();
        var invitation = OrganizationInvitation.CreateWithId(invitationId, organization.Id, "recipient@example.test", user.Id, now, now.AddDays(7), new byte[32], deliveryId);
        var delivery = OrganizationInvitationDelivery.Create(organization.Id, invitationId, deliveryId, now, now.AddDays(7));
        db.Users.Add(user);
        db.Organizations.Add(organization);
        db.OrganizationInvitations.Add(invitation);
        db.OrganizationInvitationDeliveries.Add(delivery);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var store = new InvitationDeliveryStatusStore(db);
        var uow = new IdentityUnitOfWork(db);
        var newer = new OrganizationInvitationEmailStatusV1(Guid.CreateVersion7(), deliveryId, "notification", 2, now, "provider-accepted", null);
        var older = new OrganizationInvitationEmailStatusV1(Guid.CreateVersion7(), deliveryId, "notification", 1, now, "queued", null);
        async Task Apply(OrganizationInvitationEmailStatusV1 message) => await uow.ExecuteInTransactionAsync(async ct => { await store.RecordAsync(message, ct); return true; });
        await Apply(newer);
        await Apply(newer);
        await Apply(older);
        Assert.Equal("provider-accepted", (await db.OrganizationInvitationDeliveries.AsNoTracking().SingleAsync()).Status);
        await Assert.ThrowsAsync<ConflictException>(() => Apply(newer with { Status = "failed" }));
        await db.OrganizationInvitations.Where(x => x.Id == invitationId).ExecuteUpdateAsync(x => x.SetProperty(i => i.CurrentDeliveryMessageId, Guid.CreateVersion7()));
        await Apply(new OrganizationInvitationEmailStatusV1(Guid.CreateVersion7(), deliveryId, "notification", 3, now, "delivered", null));
        Assert.Equal("provider-accepted", (await db.OrganizationInvitationDeliveries.AsNoTracking().SingleAsync()).Status);
    }

    [DeliveryDatabaseFact]
    public async Task DispatchFailsClosedWhenAuthorityUnavailableAndRechecksRetry()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        var eligibility = new FakeEligibility();
        var provider = new FakeProvider();
        await using var services = fixture.Services(s =>
        {
            s.AddSingleton<IInvitationDispatchEligibility>(eligibility);
            s.AddSingleton<IEmailProvider>(provider);
        });
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var request = InvitationEmailEnvelopeFactory.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), now, now.AddDays(7), new OrganizationInvitationEmailData("recipient@example.test", "Workspace", ["Member"], new string('a', 43)), "v1", fixture.Key.ExportSubjectPublicKeyInfoPem());
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new AcceptInvitationDeliveryCommand(request), CancellationToken.None);
        }

        var worker = new EmailDeliveryWorker(services.GetRequiredService<IServiceScopeFactory>(), NullLogger<EmailDeliveryWorker>.Instance);
        await worker.RunOnceAsync(CancellationToken.None);
        Assert.Equal(0, provider.Calls);
        await using (var db = fixture.Notification())
        {
            var job = await db.EmailDeliveries.SingleAsync();
            Assert.Equal("Pending", job.Status);
            Assert.Equal("AuthorityUnavailable", job.FailureCode);
            await db.Database.ExecuteSqlRawAsync("UPDATE notification.\"EmailDeliveries\" SET \"NextAttemptAt\" = now(); UPDATE notification.\"EmailDeliveryControls\" SET \"NextRequestAt\" = now()");
        }

        eligibility.Allowed = true;
        await worker.RunOnceAsync(CancellationToken.None);
        Assert.Equal(1, provider.Calls);
        Assert.Equal(2, eligibility.Calls);
        await using var verify = fixture.Notification();
        Assert.Equal("ProviderAccepted", (await verify.EmailDeliveries.SingleAsync()).Status);
        Assert.Equal(3, await verify.Set<OutboxMessage>().CountAsync(x => x.MessageType == OrganizationInvitationEmailStatusV1.EventType));
        verify.EmailWebhookReceipts.Add(new DeliveryPilot.Notification.Domain.Models.EmailWebhookReceipt
        {
            EventId = "verified-delivery-event",
            PayloadHash = new string('A', 64),
            ProviderId = "provider-test",
            EventType = "email.delivered",
            ReceivedAt = DateTimeOffset.UtcNow,
        });
        await verify.SaveChangesAsync();
        await worker.RunOnceAsync(CancellationToken.None);
        verify.ChangeTracker.Clear();
        Assert.Equal("Delivered", (await verify.EmailDeliveries.SingleAsync()).Outcome);
        var statuses = await verify.Set<OutboxMessage>().Where(x => x.MessageType == OrganizationInvitationEmailStatusV1.EventType).OrderBy(x => x.AggregateRevision).ToListAsync();
        Assert.Equal(4, statuses.Count);
        Assert.Equal("delivered", JsonSerializer.Deserialize<OrganizationInvitationEmailStatusV1>(statuses[^1].PayloadJson)!.Status);
    }

    [DeliveryDatabaseFact]
    public async Task InvitationIntakeDeduplicatesAndStagesOneStatus()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        await using var services = fixture.Services();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var request = InvitationEmailEnvelopeFactory.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), now, now.AddDays(7), new OrganizationInvitationEmailData("recipient@example.test", "Workspace", ["Member"], new string('a', 43)), "v1", fixture.Key.ExportSubjectPublicKeyInfoPem());
        await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            await using var scope = services.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new AcceptInvitationDeliveryCommand(request), CancellationToken.None);
            Assert.True(result.IsSuccess);
        }));
        await using var db = fixture.Notification();
        var job = await db.EmailDeliveries.SingleAsync();
        Assert.Equal("organization-invitation", job.RequestKind);
        Assert.Equal(1, job.InvitationStatusSequence);
        Assert.Single(await db.Set<InboxMessage>().ToListAsync());
        Assert.Single(await db.Set<OutboxMessage>().Where(x => x.MessageType == OrganizationInvitationEmailStatusV1.EventType).ToListAsync());
        Assert.Equal(4, await db.Set<OutboxMessage>().CountAsync(x => x.MessageType == EmailDeliveryRecordedV1.EventType));
    }

    [Fact]
    public void ProducerDoesNotExposeRecipientProofOrRoleNames()
    {
        using var rsa = RSA.Create(3072);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var data = new OrganizationInvitationEmailData("private@example.test", "Workspace", ["Sensitive role"], new string('a', 43));
        var request = InvitationEmailEnvelopeFactory.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), now, now.AddDays(7), data, "test", rsa.ExportSubjectPublicKeyInfoPem());
        string json = JsonSerializer.Serialize(request);
        Assert.DoesNotContain(data.Recipient, json, StringComparison.Ordinal);
        Assert.DoesNotContain(data.Proof, json, StringComparison.Ordinal);
        Assert.DoesNotContain(data.OfferedRoleNames[0], json, StringComparison.Ordinal);
        byte[] plaintext = EnvelopeEncryption.Decrypt(new(request.Payload.WrappedKey, request.Payload.Nonce, request.Payload.Tag, request.Payload.Ciphertext), request.AssociatedData(), rsa.ExportPkcs8PrivateKeyPem());
        try
        {
            var decoded = JsonSerializer.Deserialize<OrganizationInvitationEmailData>(plaintext)!;
            Assert.Equal(data.Recipient, decoded.Recipient);
            Assert.Equal(data.Proof, decoded.Proof);
            Assert.Equal(data.OfferedRoleNames, decoded.OfferedRoleNames);
            RenderedEmail rendered = OrganizationInvitationTemplate.Render(request, plaintext, new EmailDeliveryOptions { Sender = "sender@example.test", FrontendOrigin = "https://app.example.test" });
            Assert.Contains("/join-workspace#invitationId=", rendered.Text, StringComparison.Ordinal);
            Assert.Contains("&proof=", rendered.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("?proof=", rendered.Text, StringComparison.Ordinal);
            Assert.Throws<ArgumentException>(() => OrganizationInvitationTemplate.Render(request, plaintext, new EmailDeliveryOptions { FrontendOrigin = "https://app.example.test/elsewhere" }));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    [Fact]
    public void InvitationEnvelopeAuthenticatesScopeAndGenerationMetadata()
    {
        using var rsa = RSA.Create(3072);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var blank = new EncryptedEmailPayload(1, "test", "x", "x", "x", "x");
        var request = new OrganizationInvitationEmailRequestedV1(Guid.CreateVersion7(), Guid.CreateVersion7(), null, "identity", Guid.CreateVersion7(), Guid.CreateVersion7(), now, now.AddDays(7), blank);
        byte[] plain = Encoding.UTF8.GetBytes("secret test payload");
        CipherEnvelope cipher = EnvelopeEncryption.Encrypt(plain, request.AssociatedData(), rsa.ExportSubjectPublicKeyInfoPem());
        request = request with { Payload = new EncryptedEmailPayload(1, "test", cipher.WrappedKey, cipher.Nonce, cipher.Tag, cipher.Ciphertext) };
        request.Validate();
        Assert.Equal(plain, EnvelopeEncryption.Decrypt(cipher, request.AssociatedData(), rsa.ExportPkcs8PrivateKeyPem()));
        Assert.ThrowsAny<CryptographicException>(() => EnvelopeEncryption.Decrypt(cipher, (request with { OrganizationId = Guid.CreateVersion7() }).AssociatedData(), rsa.ExportPkcs8PrivateKeyPem()));
        Assert.ThrowsAny<CryptographicException>(() => EnvelopeEncryption.Decrypt(cipher, (request with { CorrelationId = Guid.CreateVersion7() }).AssociatedData(), rsa.ExportPkcs8PrivateKeyPem()));
        Assert.Throws<ArgumentException>(() => (request with { ExpiresAt = now.AddDays(8) }).Validate());
        Assert.DoesNotContain("secret", request.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void StatusRequiresTrustedProducerAndPositiveSequence()
    {
        var status = new OrganizationInvitationEmailStatusV1(Guid.CreateVersion7(), Guid.CreateVersion7(), "notification", 1, DateTimeOffset.UtcNow, "provider-accepted", null);
        status.Validate();
        Assert.Throws<ArgumentException>(() => (status with { Producer = "identity" }).Validate());
        Assert.Throws<ArgumentException>(() => (status with { Sequence = 0 }).Validate());
        Assert.Throws<ArgumentException>(() => (status with { Status = "delivered-to-someone" }).Validate());
        Assert.Throws<ArgumentException>(() => (status with { FailureCode = "RecipientEmailLikeValue" }).Validate());
    }

    private sealed class FakeEligibility : IInvitationDispatchEligibility
    {
        public bool? Allowed { get; set; }
        public int Calls { get; private set; }
        public Task<bool?> IsAllowedAsync(Guid deliveryId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Allowed);
        }
    }

    private sealed class FakeProvider : IEmailProvider
    {
        public int Calls { get; private set; }
        public Task<ProviderResult> SendAsync(Guid id, string frozenJson, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new ProviderResult("Accepted", "provider-test"));
        }
    }
}
