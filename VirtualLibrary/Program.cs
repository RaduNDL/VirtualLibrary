using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;
using Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore;
using VirtualLibrary.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("AppDbContextConnection")
    ?? throw new InvalidOperationException("Connection string 'AppDbContextConnection' not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString, sql =>
        sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null));
});

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services
    .AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

builder.Services.AddRazorPages()
#if DEBUG
    .AddMvcOptions(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
#endif
    ;

builder.Services.AddHttpClient();
builder.Services.AddHttpClient<PdfService>();
builder.Services.AddScoped<BookImporter>();
builder.Services.AddScoped<AudiobookService>();
builder.Services.AddScoped<PdfService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    try
    {
        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

        // Create Roles
        string[] roleNames = { "Administrator", "Client" };
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var r = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (!r.Succeeded)
                    logger.LogWarning("Role '{Role}' create failed: {Errors}", roleName, string.Join(", ", r.Errors.Select(e => e.Description)));
            }
        }

        const string adminEmail = "admin@gmail.com";
        const string adminPassword = "Admin123!";

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            var newAdminUser = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(newAdminUser, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(newAdminUser, "Administrator");
                logger.LogInformation("Admin user created successfully. Email: {Email}, Password: {Password}", adminEmail, adminPassword);
            }
            else
            {
                logger.LogWarning("Admin user creation failed: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            logger.LogInformation("Admin user already exists. Email: {Email}", adminEmail);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred during seeding the database.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

var audioBooks = Path.Combine(app.Environment.WebRootPath, "audiobooks");
var pdfs = Path.Combine(app.Environment.WebRootPath, "pdfs");

if (!Directory.Exists(audioBooks))
    Directory.CreateDirectory(audioBooks);

if (!Directory.Exists(pdfs))
    Directory.CreateDirectory(pdfs);

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();