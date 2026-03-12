using InvoiceWizard.Backend.Domain;
using Microsoft.EntityFrameworkCore;

namespace InvoiceWizard.Backend.Data;

public static class DatabaseInitializer
{
    public static async Task SeedAsync(InvoiceWizardDbContext db)
    {
        if (!await db.SubscriptionPlans.AnyAsync())
        {
            db.SubscriptionPlans.AddRange(
                new SubscriptionPlan
                {
                    Code = "starter",
                    Name = "Starter",
                    MaxUsers = 1,
                    MaxProjects = 25,
                    MaxCustomers = 100,
                    IncludesMobileAccess = true
                },
                new SubscriptionPlan
                {
                    Code = "business",
                    Name = "Business",
                    MaxUsers = 5,
                    MaxProjects = 250,
                    MaxCustomers = 1000,
                    IncludesMobileAccess = true
                });

            await db.SaveChangesAsync();
        }
    }

    public static string CreateSlug(string value)
    {
        var slug = new string(value.Trim().ToLowerInvariant().Select(ch =>
            char.IsLetterOrDigit(ch) ? ch : '-').ToArray());

        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug.Trim('-');
    }
}
