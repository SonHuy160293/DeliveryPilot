namespace DeliveryPilot.SharedKernel.Security;

public interface ICurrentUserProvider
{
    CurrentUser? GetCurrentUser();
}
