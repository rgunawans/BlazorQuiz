using Microsoft.AspNetCore.Identity;
using BlazorQuiz.Data;

namespace BlazorQuiz.Data
{
    public static class DataSeeder
    {
        // Development-only fallbacks. Outside Development the accounts are created only
        // when Seed:AdminEmail / Seed:AdminPassword (and optionally Seed:UserEmail /
        // Seed:UserPassword) are configured, e.g. via environment variables Seed__AdminPassword.
        private const string DevAdminEmail = "admin@blazorquiz.com";
        private const string DevAdminPassword = "Admin123!";
        private const string DevUserEmail = "user@blazorquiz.com";
        private const string DevUserPassword = "User123!";

        public static async Task SeedDefaultUserAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<DataSeederMarker>>();
            var isDev = env.IsDevelopment();

            // Create roles if they don't exist
            await CreateRoleIfNotExistsAsync(roleManager, "Admin");
            await CreateRoleIfNotExistsAsync(roleManager, "User");

            var adminEmail = config["Seed:AdminEmail"] ?? (isDev ? DevAdminEmail : null);
            var adminPassword = config["Seed:AdminPassword"] ?? (isDev ? DevAdminPassword : null);
            if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
            {
                await CreateDefaultUserAsync(userManager, logger, adminEmail, adminPassword, "Admin");
            }
            else
            {
                logger.LogWarning("No admin account seeded: set Seed:AdminEmail and Seed:AdminPassword to create one.");
            }

            var userEmail = config["Seed:UserEmail"] ?? (isDev ? DevUserEmail : null);
            var userPassword = config["Seed:UserPassword"] ?? (isDev ? DevUserPassword : null);
            if (!string.IsNullOrWhiteSpace(userEmail) && !string.IsNullOrWhiteSpace(userPassword))
            {
                await CreateDefaultUserAsync(userManager, logger, userEmail, userPassword, "User");
            }
        }

        private static async Task CreateRoleIfNotExistsAsync(RoleManager<IdentityRole> roleManager, string roleName)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
            }
        }

        private static async Task CreateDefaultUserAsync(UserManager<ApplicationUser> userManager, ILogger logger, string email, string password, string role)
        {
            var existingUser = await userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                logger.LogInformation("{Role} user already exists: {Email}", role, email);
                return;
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true // Auto-confirm email for seeded users
            };

            var result = await userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, role);
                logger.LogInformation("{Role} user created: {Email}", role, email);
            }
            else
            {
                logger.LogError("Failed to create {Role} user: {Errors}", role, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        // Non-static type argument for ILogger<T>, which cannot use a static class.
        private sealed class DataSeederMarker { }
    }
}
