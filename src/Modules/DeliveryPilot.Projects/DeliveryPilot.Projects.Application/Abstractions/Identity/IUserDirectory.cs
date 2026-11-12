namespace DeliveryPilot.Projects.Application.Abstractions.Identity;

public interface IUserDirectory
{
    Task<IReadOnlyDictionary<Guid, string>> LookupDisplayNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}
