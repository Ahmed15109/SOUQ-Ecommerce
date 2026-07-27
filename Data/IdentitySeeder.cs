using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using EcommerceApp.Helpers;
using EcommerceApp.Models;

namespace EcommerceApp.Data
{
    public static class IdentitySeeder
    {
        public static async Task SeedAsync(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration configuration,
            ILogger? logger = null)
        {
            string[] roles = [AppRoles.Admin, AppRoles.SuperAdmin, AppRoles.User];
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
                    if (!roleResult.Succeeded)
                    {
                        throw new InvalidOperationException(
                            $"Failed to create role '{role}': {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
                    }
                }
            }

            var superAdminSection = configuration.GetSection("SuperAdmin");
            var superEmail = superAdminSection["Email"]?.Trim();
            var superPassword = superAdminSection["Password"];

            if (string.IsNullOrWhiteSpace(superEmail) || string.IsNullOrWhiteSpace(superPassword))
            {
                logger?.LogWarning("SuperAdmin credentials (Email/Password) are not configured. Skipping initial SuperAdmin seeding. Configure via User Secrets or Environment Variables (SuperAdmin__Email, SuperAdmin__Password).");
                return;
            }

            var superUser = await userManager.FindByEmailAsync(superEmail);
            var createdSuperUser = false;
            if (superUser == null)
            {
                superUser = new ApplicationUser
                {
                    UserName = superEmail,
                    Email = superEmail,
                    EmailConfirmed = true,
                    FullName = "Super Admin"
                };

                var result = await userManager.CreateAsync(superUser, superPassword);
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Failed to create the configured SuperAdmin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }

                createdSuperUser = true;
                logger?.LogInformation("Successfully created initial SuperAdmin user: {Email}", superEmail);
            }
            else if (!await userManager.IsInRoleAsync(superUser, AppRoles.SuperAdmin))
            {
                throw new InvalidOperationException(
                    "The configured SuperAdmin email belongs to an existing account that is not already a SuperAdmin. " +
                    "Refusing to elevate that account automatically.");
            }

            if (!await userManager.IsInRoleAsync(superUser, AppRoles.SuperAdmin))
            {
                var result = await userManager.AddToRoleAsync(superUser, AppRoles.SuperAdmin);
                if (!result.Succeeded)
                {
                    if (createdSuperUser)
                    {
                        await userManager.DeleteAsync(superUser);
                    }

                    throw new InvalidOperationException(
                        $"Failed to assign SuperAdmin role: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }

            if (!await userManager.IsInRoleAsync(superUser, AppRoles.Admin))
            {
                var result = await userManager.AddToRoleAsync(superUser, AppRoles.Admin);
                if (!result.Succeeded)
                {
                    if (createdSuperUser)
                    {
                        await userManager.DeleteAsync(superUser);
                    }

                    throw new InvalidOperationException(
                        $"Failed to assign Admin role: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
        }
    }
}
