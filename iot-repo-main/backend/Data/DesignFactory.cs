using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Luggage;
public class DesignFactory : IDesignTimeDbContextFactory<Store>
{
    public Store CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<Store>()
        .UseNpgsql("Host=localhost;Database=luggage;Username=luggage").Options);
}
