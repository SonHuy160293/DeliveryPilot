namespace DeliveryPilot.Identity.Contracts.Authority;

public sealed record UserEmailMatchResult(bool Matches, DateTimeOffset EvaluatedAt)
{
    public override string ToString() => nameof(UserEmailMatchResult);
}
