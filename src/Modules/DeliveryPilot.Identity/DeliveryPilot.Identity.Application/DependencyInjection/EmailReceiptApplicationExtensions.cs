using DeliveryPilot.Identity.Application.Features.Messaging;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace DeliveryPilot.Identity.Application.DependencyInjection;

public static class EmailReceiptApplicationExtensions
{
    public static IServiceCollection AddEmailReceiptApplication(this IServiceCollection services)
    {
        // Register only receipt handling in this independently hosted worker.
        services.AddSelfMadeCqrs(_ => { }).AddApplicationPipelineBehaviors();
        services.AddScoped<IRequestHandler<RecordEmailDeliveryReceiptCommand, Result<bool>>, RecordEmailDeliveryReceiptHandler>();
        return services;
    }
}
