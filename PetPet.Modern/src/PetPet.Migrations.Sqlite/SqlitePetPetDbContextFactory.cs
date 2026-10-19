using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PetPet.Infrastructure.Data;

namespace PetPet.Migrations.Sqlite;

public class SqlitePetPetDbContextFactory : IDesignTimeDbContextFactory<PetPetDbContext>
{
    public PetPetDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PetPetDbContext>();
        optionsBuilder.UseSqlite("Data Source=petpet-design.db", b =>
            b.MigrationsAssembly(typeof(SqlitePetPetDbContextFactory).Assembly.GetName().Name));
        return new PetPetDbContext(optionsBuilder.Options);
    }
}
