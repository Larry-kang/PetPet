using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PetPet.Application.Members;
using PetPet.Domain.Entities;
using PetPet.Infrastructure.Data;

namespace PetPet.Infrastructure.Services;

public class MemberRepository(PetPetDbContext context) : IMemberRepository
{
    public Task<Member?> FindAsync(string email) =>
        context.Members.SingleOrDefaultAsync(m => m.Email == email);

    public Task<Member?> FindByNormalizedEmailAsync(string normalizedEmail) =>
        context.Members.SingleOrDefaultAsync(m => m.NormalizedEmail == normalizedEmail);

    public Task<Member?> FindByPublicIdAsync(Guid publicId) =>
        context.Members.SingleOrDefaultAsync(m => m.PublicId == publicId);

    public Task SaveAsync() => context.SaveChangesAsync();

    public async Task<IReadOnlyList<Member>> ListAsync() =>
        await context.Members.AsNoTracking().ToListAsync();

    public async Task<bool> AddAsync(Member member)
    {
        if (await context.Members.AnyAsync(m => m.NormalizedEmail == member.NormalizedEmail || m.Email == member.Email))
            return false;

        context.Members.Add(member);
        try
        {
            await context.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is SqlException { Number: 2601 or 2627 } ||
            ex.InnerException is SqliteException { SqliteErrorCode: 19 })
        {
            context.Entry(member).State = EntityState.Detached;
            return false;
        }
    }
}
