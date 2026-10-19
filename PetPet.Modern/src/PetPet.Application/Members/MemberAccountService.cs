using System.ComponentModel.DataAnnotations;
using PetPet.Domain.Entities;

namespace PetPet.Application.Members;

public class RegisterMember(string email, string password, string name, string phone)
{
    public RegisterMember() : this("", "", "", "") { }
    [Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = email;
    [Required, StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = password;
    [Required, StringLength(40)]
    public string Name { get; set; } = name;
    [StringLength(20)]
    public string Phone { get; set; } = phone;
}

public interface IMemberRepository
{
    Task<Member?> FindAsync(string email);
    Task<Member?> FindByNormalizedEmailAsync(string normalizedEmail);
    Task<Member?> FindByPublicIdAsync(Guid publicId);
    Task<bool> AddAsync(Member member);
    Task SaveAsync();
    Task<IReadOnlyList<Member>> ListAsync();
}

public enum PasswordCheck { Failed, Success, RehashNeeded }

public interface IMemberPasswordHasher
{
    string Hash(Member member, string password);
    PasswordCheck Verify(Member member, string password);
}

public class MemberAccountService(IMemberRepository members, IMemberPasswordHasher passwords)
{
    public async Task<bool> RegisterAsync(RegisterMember input)
    {
        Validator.ValidateObject(input, new ValidationContext(input), validateAllProperties: true);
        var normalized = input.Email.Trim().ToUpperInvariant();
        var member = new Member
        {
            Email = input.Email.Trim(),
            NormalizedEmail = normalized,
            PublicId = Guid.NewGuid(),
            Name = input.Name.Trim(),
            Phone = input.Phone?.Trim() ?? string.Empty,
            Birthday = DateTime.UtcNow,
            CityId = 1,
            Gender = true,
            IsEnabled = true,
            IsMatchEnabled = false,
            Photo = "default.jpg",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            Version = Guid.NewGuid()
        };
        member.Password = passwords.Hash(member, input.Password);
        return await members.AddAsync(member);
    }

    public async Task<Member?> AuthenticateAsync(string? email, string? password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password) || password.Length > 128)
            return null;
        var normalized = email.Trim().ToUpperInvariant();
        var member = await members.FindByNormalizedEmailAsync(normalized) ?? await members.FindAsync(email.Trim());
        if (member is null || !member.IsEnabled) return null;
        var result = passwords.Verify(member, password);
        if (result == PasswordCheck.Failed) return null;
        if (result == PasswordCheck.RehashNeeded)
        {
            member.Password = passwords.Hash(member, password);
            await members.SaveAsync();
        }
        return member;
    }

    public Task<IReadOnlyList<Member>> ListAsync() => members.ListAsync();
}
