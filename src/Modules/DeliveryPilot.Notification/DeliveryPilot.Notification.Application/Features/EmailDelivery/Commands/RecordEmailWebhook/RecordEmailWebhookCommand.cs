using DeliveryPilot.Notification.Application.Abstractions;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Notification.Application.Features.EmailDelivery.Commands.RecordEmailWebhook;

public sealed record RecordEmailWebhookCommand(ProviderEmailEvent Event) : ICommand<bool>;
