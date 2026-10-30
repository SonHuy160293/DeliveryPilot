using DeliveryPilot.AspNetCore.HttpClient.DependencyInjection;
using DeliveryPilot.Messaging.Configuration;
using DeliveryPilot.Messaging.MassTransit;
using DeliveryPilot.Messaging.Outbox;
using DeliveryPilot.Notification.Application.Abstractions;
using DeliveryPilot.Notification.Contracts;
using DeliveryPilot.Notification.Infrastructure.Email;
using DeliveryPilot.Notification.Infrastructure.Email.Providers;
using DeliveryPilot.Notification.Infrastructure.Email.Workers;
using DeliveryPilot.Notification.Infrastructure.Messaging.Consumers;
using DeliveryPilot.Notification.Infrastructure.Persistence;
using DeliveryPilot.Notification.Infrastructure.Security;
using DeliveryPilot.SharedKernel.Abstractions.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeliveryPilot.Notification.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddNotificationWebhookSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmailDeliveryOptions>().Bind(configuration.GetSection("Notification:EmailDelivery")).Validate(x => x.WebhookValid(), "Missing callback verification secrets.").ValidateOnStart();
        services.AddSingleton<EmailPayloadProtection>();
        services.AddSingleton<ResendWebhookVerifier>();
        return services;
    }

    public static IServiceCollection AddNotificationPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        string connection = configuration.GetConnectionString("NotificationDb") ?? throw new InvalidOperationException("Notification database connection required.");
        if (configuration["Database:PasswordFile"] is { Length: > 0 } passwordFile)
        {
            connection = new Npgsql.NpgsqlConnectionStringBuilder(connection)
            {
                Password = File.ReadAllText(passwordFile).Trim()
            }.ConnectionString;
        }

        services.AddDbContext<NotificationDbContext>(options => options.UseNpgsql(connection));
        services.AddDbContext<NotificationInboxDbContext>(options => options.UseNpgsql(connection));
        services.AddScoped<IUnitOfWork, NotificationUnitOfWork>();
        services.AddScoped<IEmailDeliveryRepository, EmailDeliveryRepository>();
        return services;
    }

    public static IServiceCollection AddNotificationEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<IInvitationDispatchEligibility, InvitationDispatchEligibility>(client => client.Timeout = TimeSpan.FromSeconds(2))
            .RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false, ConnectTimeout = TimeSpan.FromSeconds(2) });
        var options = configuration.GetSection("Notification:EmailDelivery").Get<EmailDeliveryOptions>() ?? new();
        services.AddOptions<EmailDeliveryOptions>().Bind(configuration.GetSection("Notification:EmailDelivery")).Validate(x => x.Valid(), "Invalid email delivery configuration.").ValidateOnStart();
        services.AddSingleton<EmailPayloadProtection>();
        services.AddSingleton<ResendWebhookVerifier>();
        if (options.Mode != "Resend")
        {
            if (options.Mode == "Mailpit")
            {
                services.AddScoped<IEmailProvider, MailpitEmailProvider>();
            }
            else
            {
                services.AddSingleton<IEmailProvider, DisabledEmailProvider>();
            }

            return services;
        }

        var transport = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Email:BaseUrl"] = "https://api.resend.com/", ["Email:ApiKeyHeaderName"] = "Authorization", ["Email:ApiKey"] = options.Mode == "Resend" && File.Exists(options.ApiKeyFile) ? "Bearer " + File.ReadAllText(options.ApiKeyFile).Trim() : "", }).Build();
        services.AddIntegrationHttpClient<IEmailProvider, EmailProvider>("Notification.Email", transport.GetSection("Email")).RemoveAllLoggers().ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false, ConnectTimeout = TimeSpan.FromSeconds(3) });
        return services;
    }

    public static IServiceCollection AddNotificationMessaging(this IServiceCollection services, IConfiguration configuration, bool runWorkers = true)
    {
        var broker = configuration.GetSection("Messaging").Get<MessagingOptions>() ?? new();
        services.Configure<MassTransitHostOptions>(options =>
        {
            options.WaitUntilStarted = true;
            options.StartTimeout = TimeSpan.FromSeconds(60);
            options.StopTimeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IOutboxDispatchStore, EfOutboxDispatchStore<NotificationDbContext>>();
        services.AddScoped<IIntegrationEventPublisher, MassTransitEventPublisher>();
        services.AddSingleton(new IntegrationEventRegistration(EmailDeliveryRecordedV1.EventType, typeof(EmailDeliveryRecordedV1)));
        services.AddSingleton(new IntegrationEventRegistration(OrganizationInvitationEmailStatusV1.EventType, typeof(OrganizationInvitationEmailStatusV1), value => ((OrganizationInvitationEmailStatusV1)value).Validate()));
        services.AddMassTransit(x =>
        {
            x.AddConsumer<EmailDeliveryRequestedConsumer>();
            x.AddConsumer<InvitationEmailRequestedConsumer>();
            x.UsingRabbitMq((context, bus) =>
            {
                RabbitConfiguration.ConfigureHost(bus, broker);
                bus.OverrideDefaultBusEndpointQueueName("deliverypilot.notification.worker.bus." + Guid.NewGuid().ToString("N"));
                bus.Message<EmailDeliveryRequestedV1>(m => m.SetEntityName("deliverypilot.notification.email-requested.v1"));
                bus.Message<EmailDeliveryRecordedV1>(m => m.SetEntityName("deliverypilot.notification.email-recorded.v1"));
                bus.Message<OrganizationInvitationEmailRequestedV1>(m => m.SetEntityName("deliverypilot.notification.organization-invitation-email-requested.v1"));
                bus.Message<OrganizationInvitationEmailStatusV1>(m => m.SetEntityName("deliverypilot.notification.organization-invitation-email-status.v1"));
                if (runWorkers)
                {
                    bus.ReceiveEndpoint("deliverypilot.notification.email-requested.v1", endpoint =>
                    {
                        RabbitConfiguration.ConfigureQueue(endpoint, broker);
                        endpoint.ConfigureConsumer<EmailDeliveryRequestedConsumer>(context);
                    });
                    bus.ReceiveEndpoint("deliverypilot.notification.organization-invitation-email-requested.v1", endpoint =>
                    {
                        RabbitConfiguration.ConfigureQueue(endpoint, broker);
                        endpoint.ConfigureConsumer<InvitationEmailRequestedConsumer>(context);
                    });
                }
            });
        });
        if (runWorkers)
        {
            services.AddHostedService<OutboxRelay>();
            services.AddHostedService<EmailDeliveryWorker>();
            services.AddHostedService<DeliveryMaintenanceWorker>();
        }

        return services;
    }
}
