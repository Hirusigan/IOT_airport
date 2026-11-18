using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Luggage;

public class DesignFactory : IDesignTimeDbContextFactory<TrackingDbContext>
{
    public TrackingDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<TrackingDbContext>()
        .UseNpgsql("Host=localhost;Database=luggage;Username=luggage").Options);
}
