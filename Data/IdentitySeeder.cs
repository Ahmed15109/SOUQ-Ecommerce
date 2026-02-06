
using Microsoft.AspNetCore.Identity;
using EcommerceApp.Models;

namespace EcommerceApp.Data
{
    public static class IdentitySeeder
    {
        public static async Task SeedAsync(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, IConfiguration configuration)
        {
            string[] roles = { "Admin", "SuperAdmin", "User" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            var adminEmail = "admin@shopnow.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true, FullName = "Default Admin" };
                var result = await userManager.CreateAsync(adminUser, "Admin@12345");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }

            var superAdminSection = configuration.GetSection("SuperAdmin");
            var superEmail = superAdminSection["Email"];
            var superPassword = superAdminSection["Password"];

            if (!string.IsNullOrEmpty(superEmail) && !string.IsNullOrEmpty(superPassword))
            {
                var superUser = await userManager.FindByEmailAsync(superEmail);
                
                if (superUser == null)
                {
                    superUser = new ApplicationUser { UserName = superEmail, Email = superEmail, EmailConfirmed = true, FullName = "Super Admin" };
                    var result = await userManager.CreateAsync(superUser, superPassword);
                    if (!result.Succeeded)
                    {
                        return;
                    }
                }

                if (!await userManager.IsInRoleAsync(superUser, "Admin"))
                {
                    await userManager.AddToRoleAsync(superUser, "Admin");
                }
                
                if (!await userManager.IsInRoleAsync(superUser, "SuperAdmin"))
                {
                    await userManager.AddToRoleAsync(superUser, "SuperAdmin");
                }
            }
        }
    }
}
