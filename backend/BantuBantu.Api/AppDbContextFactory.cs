using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace BantuBantu.Infrastructure;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Default") ?? throw new InvalidOperationException("Set ConnectionStrings__Default for EF commands.");
        return new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    }
}
