namespace DeliveryPilot.Identity.Contracts;

public sealed record UserLookupResult(IReadOnlyList<UserIntegrationDto> Users)
{
    public override string ToString() => nameof(UserLookupResult);
}
