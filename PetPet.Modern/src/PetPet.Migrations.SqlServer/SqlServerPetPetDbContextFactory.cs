using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PetPet.Infrastructure.Data;

namespace PetPet.Migrations.SqlServer;

public class SqlServerPetPetDbContextFactory : IDesignTimeDbContextFactory<PetPetDbContext>
{
    public PetPetDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PetPetDbContext>();
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=PetPetDesign;Trusted_Connection=True;", b =>
            b.MigrationsAssembly(typeof(SqlServerPetPetDbContextFactory).Assembly.GetName().Name));
        return new PetPetDbContext(optionsBuilder.Options);
    }
}
