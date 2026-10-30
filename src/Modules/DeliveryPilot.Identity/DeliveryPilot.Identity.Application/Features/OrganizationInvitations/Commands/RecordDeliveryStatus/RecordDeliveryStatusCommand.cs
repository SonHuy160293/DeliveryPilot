using DeliveryPilot.Notification.Contracts;
using DeliveryPilot.SharedKernel.Abstractions.Message;

namespace DeliveryPilot.Identity.Application.Features.OrganizationInvitations.Commands.RecordDeliveryStatus;

public sealed record RecordDeliveryStatusCommand(OrganizationInvitationEmailStatusV1 Event) : ICommand<bool>;
