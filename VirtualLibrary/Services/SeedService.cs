using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;

namespace VirtualLibrary.Services
{
    public class SeedService
    {
        private readonly AppDbContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<SeedService> _logger;

        public SeedService(
            AppDbContext context,
            RoleManager<IdentityRole> roleManager,
            UserManager<IdentityUser> userManager,
            ILogger<SeedService> logger)
        {
            _context = context;
            _roleManager = roleManager;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            try
            {
                await _context.Database.MigrateAsync();
                _logger.LogInformation("Database migrated successfully");

                await SeedRolesAsync();

                await SeedAdminUserAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during seeding the database.");
                throw;
            }
        }

        private async Task SeedRolesAsync()
        {
            string[] roleNames = { "Administrator", "Client" };

            foreach (var roleName in roleNames)
            {
                var roleExists = await _roleManager.RoleExistsAsync(roleName);
                if (!roleExists)
                {
                    var result = await _roleManager.CreateAsync(new IdentityRole(roleName));
                    if (result.Succeeded)
                    {
                        _logger.LogInformation("Role '{Role}' created successfully", roleName);
                    }
                    else
                    {
                        _logger.LogWarning("Role '{Role}' creation failed: {Errors}", roleName,
                            string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }
                else
                {
                    _logger.LogInformation("Role '{Role}' already exists", roleName);
                }
            }
        }

        private async Task SeedAdminUserAsync()
        {
            const string adminEmail = "admin@gmail.com";
            const string adminPassword = "Parola123!";
            const string adminRole = "Administrator";

            var adminUser = await _userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                var newAdminUser = new IdentityUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(newAdminUser, adminPassword);

                if (result.Succeeded)
                {
                    var roleResult = await _userManager.AddToRoleAsync(newAdminUser, adminRole);

                    if (roleResult.Succeeded)
                    {
                        _logger.LogInformation("Admin user created successfully. Email: {Email}", adminEmail);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to add admin role: {Errors}",
                            string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                    }
                }
                else
                {
                    _logger.LogWarning("Admin user creation failed: {Errors}",
                        string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                _logger.LogInformation("Admin user already exists. Email: {Email}", adminEmail);
            }
        }
    }
}