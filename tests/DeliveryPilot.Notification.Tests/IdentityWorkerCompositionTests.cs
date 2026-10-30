using DeliveryPilot.Identity.Application.Abstractions.Messaging;
using DeliveryPilot.Identity.Application.DependencyInjection;
using DeliveryPilot.Identity.Application.Features.Messaging;
using DeliveryPilot.Identity.Infrastructure.Messaging;
using DeliveryPilot.Identity.Infrastructure.Persistence.Core;
using DeliveryPilot.Identity.Infrastructure.Persistence.DbContexts;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Persistence;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeliveryPilot.Notification.Tests;

public sealed class IdentityWorkerCompositionTests
{
    [Fact]
    public void ReceiptOnlyWorkerValidatesWithoutRegistrationSecurityServices()
    {
        var registrations = new ServiceCollection();
        registrations.AddLogging();
        registrations.AddEmailReceiptApplication();
        registrations.AddDbContext<IdentityDbContext>(x => x.UseNpgsql("Host=localhost;Database=unused;Username=unused"));
        registrations.AddScoped<IUnitOfWork, IdentityUnitOfWork>();
        registrations.AddScoped<IEmailDeliveryReceiptStore, EmailDeliveryReceiptStore>();
        using var services = registrations.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRequestHandler<RecordEmailDeliveryReceiptCommand, Result<bool>>>());
        Assert.Single(registrations.Where(x => x.ServiceType.IsGenericType && x.ServiceType.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)));
    }
}
