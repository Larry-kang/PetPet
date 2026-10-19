using Microsoft.AspNetCore.Identity;
using PetPet.Application.Members;
using PetPet.Domain.Entities;

namespace PetPet.Infrastructure.Services;

public class MemberPasswordHasher : IMemberPasswordHasher
{
    private readonly PasswordHasher<Member> _hasher = new();

    public string Hash(Member member, string password) => _hasher.HashPassword(member, password);

    public PasswordCheck Verify(Member member, string password)
    {
        try
        {
            return _hasher.VerifyHashedPassword(member, member.Password, password) switch
            {
                PasswordVerificationResult.Success => PasswordCheck.Success,
                PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.RehashNeeded,
                _ => PasswordCheck.Failed
            };
        }
        catch (FormatException) { return PasswordCheck.Failed; }
        catch (ArgumentException) { return PasswordCheck.Failed; }
    }
}
