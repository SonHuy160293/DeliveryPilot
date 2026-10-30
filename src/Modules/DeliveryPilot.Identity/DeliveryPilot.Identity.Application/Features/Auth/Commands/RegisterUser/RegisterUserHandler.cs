using DeliveryPilot.Identity.Application.Abstractions.Repositories;
using DeliveryPilot.Identity.Application.Abstractions.Security;
using DeliveryPilot.Identity.Domain.Models;
using DeliveryPilot.SharedKernel.Abstractions.Message;
using DeliveryPilot.SharedKernel.Abstractions.Shared;
using DeliveryPilot.SharedKernel.Exceptions;

namespace DeliveryPilot.Identity.Application.Features.Auth.Commands.RegisterUser;

public sealed class RegisterUserHandler(IUserRepository users, IPasswordCredentialRepository credentials, IPasswordHashService hasher)
    : ICommandHandler<RegisterUserCommand, RegisterUserResponse>
{
    public async Task<Result<RegisterUserResponse>> HandleAsync(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        string email = User.NormalizeEmail(request.Email!);
        if (await users.ExistsForRegistrationAsync(email, cancellationToken))
            throw new ConflictException("Registration.EmailExists", "This email is exist");
        User user = User.Create(email, request.DisplayName!, status: "active");
        string hash = await hasher.HashAsync(user, request.Password!, cancellationToken);
        await users.AddAsync(user, cancellationToken);
        await credentials.AddAsync(PasswordCredential.Create(user.Id, hash, DateTimeOffset.UtcNow), cancellationToken);
        return Result.Success(new RegisterUserResponse(user.Id));
    }
}
