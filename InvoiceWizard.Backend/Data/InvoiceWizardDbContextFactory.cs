using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace InvoiceWizard.Backend.Data;

public class InvoiceWizardDbContextFactory : IDesignTimeDbContextFactory<InvoiceWizardDbContext>
{
    public InvoiceWizardDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("PostgreSql")
            ?? throw new InvalidOperationException("Connection string 'PostgreSql' is missing.");

        var optionsBuilder = new DbContextOptionsBuilder<InvoiceWizardDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new InvoiceWizardDbContext(optionsBuilder.Options);
    }
}
