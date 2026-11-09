using System.Security.Cryptography;
using DeliveryPilot.Projects.Application.Features.Engagements.Commands.IssueEngagementLinkCode;

namespace DeliveryPilot.Projects.Infrastructure.Identity;

public sealed class EngagementLinkCodeGenerator : IEngagementLinkCodeGenerator
{
    private const string _alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public string Generate()
    {
        Span<char> code = stackalloc char[32];
        for (int index = 0; index < code.Length; index++)
        {
            code[index] = _alphabet[RandomNumberGenerator.GetInt32(_alphabet.Length)];
        }

        return new string(code);
    }
}
