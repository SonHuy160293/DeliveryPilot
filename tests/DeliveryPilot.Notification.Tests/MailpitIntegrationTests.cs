using System.Text.Json;
using DeliveryPilot.Notification.Application.Features.EmailDelivery.Commands.AcceptEmailDelivery;
using DeliveryPilot.Notification.Application.Features.EmailDelivery.Commands.AcceptInvitationDelivery;
using DeliveryPilot.Notification.Application.Abstractions;
using DeliveryPilot.Notification.Contracts;
using DeliveryPilot.Identity.Infrastructure.Messaging;
using DeliveryPilot.Notification.Infrastructure.Email;
using DeliveryPilot.Notification.Infrastructure.Email.Workers;
using DeliveryPilot.Notification.Infrastructure.Persistence;
using DeliveryPilot.SharedKernel.Cqrs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeliveryPilot.Notification.Tests;
public sealed class DeliveryMailpitFactAttribute : FactAttribute
{
    public DeliveryMailpitFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DELIVERYPILOT_DELIVERY_TEST_POSTGRES")) || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DELIVERYPILOT_DELIVERY_TEST_SMTP_PORT")) || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DELIVERYPILOT_DELIVERY_TEST_MAILPIT_URL")))
        {
            Skip = "Supply disposable PostgreSQL and Mailpit endpoints to verify actual local SMTP delivery.";
        }
    }
}

public sealed class MailpitIntegrationTests
{
    [DeliveryMailpitFact]
    public async Task InvitationSmtpContainsFragmentAndOfferSnapshot()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        await using var services = fixture.Services(s =>
        {
            s.Configure<EmailDeliveryOptions>(options =>
            {
                options.MailpitPort = int.Parse(Environment.GetEnvironmentVariable("DELIVERYPILOT_DELIVERY_TEST_SMTP_PORT")!);
                options.MailpitHost = "127.0.0.1";
            });
            s.AddSingleton<IInvitationDispatchEligibility, AllowedEligibility>();
        });
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var request = InvitationEmailEnvelopeFactory.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), now, now.AddDays(7), new OrganizationInvitationEmailData("recipient@example.test", "Workspace", ["Offer name"], new string('a', 43)), "v1", fixture.Key.ExportSubjectPublicKeyInfoPem());
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new AcceptInvitationDeliveryCommand(request), default);
        }

        await new EmailDeliveryWorker(services.GetRequiredService<IServiceScopeFactory>(), NullLogger<EmailDeliveryWorker>.Instance).RunOnceAsync(default);
        await using var db = fixture.Notification();
        Assert.Equal("ProviderAccepted", (await db.EmailDeliveries.SingleAsync()).Status);
        using var client = new HttpClient { BaseAddress = new Uri(Environment.GetEnvironmentVariable("DELIVERYPILOT_DELIVERY_TEST_MAILPIT_URL")!) };
        using var list = JsonDocument.Parse(await client.GetStringAsync("api/v1/messages"));
        var message = list.RootElement.GetProperty("messages").EnumerateArray().First(x => x.GetProperty("Subject").GetString() == "Your workspace invitation");
        using var content = JsonDocument.Parse(await client.GetStringAsync("api/v1/message/" + message.GetProperty("ID").GetString()));
        string text = content.RootElement.GetProperty("Text").GetString()!;
        Assert.Contains("Offer name", text, StringComparison.Ordinal);
        Assert.Contains("/join-workspace#invitationId=", text, StringComparison.Ordinal);
        Assert.Contains("&proof=", text, StringComparison.Ordinal);
    }

    [DeliveryMailpitFact]
    public async Task RealSmtpDeliveryContainsAccessibleVerificationButtonAndPlainText()
    {
        await using var fixture = await DeliveryFixture.CreateAsync();
        await using var services = fixture.Services(s => s.Configure<EmailDeliveryOptions>(o =>
        {
            o.MailpitPort = int.Parse(Environment.GetEnvironmentVariable("DELIVERYPILOT_DELIVERY_TEST_SMTP_PORT")!);
            o.MailpitHost = "127.0.0.1";
        }));
        var request = PersistenceIntegrationTests.Request(fixture);
        await using (var scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISender>().SendAsync(new AcceptEmailDeliveryCommand(request), default);
        }

        await new EmailDeliveryWorker(services.GetRequiredService<IServiceScopeFactory>(), NullLogger<EmailDeliveryWorker>.Instance).RunOnceAsync(default);
        await using var db = fixture.Notification();
        Assert.Equal("ProviderAccepted", (await db.EmailDeliveries.SingleAsync()).Status);
        using var client = new HttpClient
        {
            BaseAddress = new(Environment.GetEnvironmentVariable("DELIVERYPILOT_DELIVERY_TEST_MAILPIT_URL")!)
        };
        using var list = JsonDocument.Parse(await client.GetStringAsync("api/v1/messages"));
        var message = list.RootElement.GetProperty("messages").EnumerateArray().First(x => x.GetProperty("Subject").GetString() == "Verify your DeliveryPilot email");
        string id = message.GetProperty("ID").GetString()!;
        using var content = JsonDocument.Parse(await client.GetStringAsync("api/v1/message/" + id));
        Assert.Contains("Verify email", content.RootElement.GetProperty("HTML").GetString());
        Assert.Contains("#token=", content.RootElement.GetProperty("Text").GetString());
    }

    private sealed class AllowedEligibility : IInvitationDispatchEligibility
    {
        public Task<bool?> IsAllowedAsync(Guid deliveryId, CancellationToken cancellationToken) => Task.FromResult<bool?>(true);
    }
}
