using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Data;
using VirtualLibrary.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("AppDbContextConnection")
    ?? throw new InvalidOperationException("Connection string 'AppDbContextConnection' not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString, sql =>
        sql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null));
});

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services
    .AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104_857_600; 
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 104_857_600; 
});

builder.Services.AddRazorPages()
#if DEBUG
    .AddMvcOptions(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
#endif
    ;

builder.Services.AddControllers();


builder.Services.AddHttpClient("PdfClient")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 10,
        AutomaticDecompression =
            System.Net.DecompressionMethods.GZip |
            System.Net.DecompressionMethods.Deflate
    });


builder.Services.AddScoped<PdfService>();
builder.Services.AddScoped<BookMetadataEnricher>();
builder.Services.AddScoped<BookPdfGenerator>();
builder.Services.AddScoped<BookImporter>();
builder.Services.AddScoped<AudiobookService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ProductDiscoveryService>();
builder.Services.AddSingleton<AudiobookQueue>();
builder.Services.AddHostedService<AudiobookWorker>();

var app = builder.Build();


var webRoot = app.Environment.WebRootPath;
var contentRoot = app.Environment.ContentRootPath;

Directory.CreateDirectory(Path.Combine(webRoot, "audiobooks"));
Directory.CreateDirectory(Path.Combine(webRoot, "pdfs"));
Directory.CreateDirectory(Path.Combine(webRoot, "pdfs", "books"));
Directory.CreateDirectory(Path.Combine(webRoot, "pdfs", "descriptions"));
Directory.CreateDirectory(Path.Combine(webRoot, "uploads"));
Directory.CreateDirectory(Path.Combine(webRoot, "uploads", "books"));
Directory.CreateDirectory(Path.Combine(webRoot, "uploads", "pdfs"));
Directory.CreateDirectory(Path.Combine(contentRoot, "GeneratedAudio"));


using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var startupLogger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    try
    {
        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

        string[] roles = { "Administrator", "Client" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(role));

                if (!roleResult.Succeeded)
                {
                    startupLogger.LogWarning(
                        "Failed creating role {Role}: {Errors}",
                        role,
                        string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                }
            }
        }

        const string adminEmail = "admin@gmail.com";
        const string adminPassword = "Parola123!";

        var admin = await userManager.FindByEmailAsync(adminEmail);

        if (admin == null)
        {
            var user = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(user, adminPassword);

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, "Administrator");
                startupLogger.LogInformation("Admin user created successfully.");
            }
            else
            {
                startupLogger.LogWarning(
                    "Admin creation failed: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            if (!await userManager.IsInRoleAsync(admin, "Administrator"))
            {
                await userManager.AddToRoleAsync(admin, "Administrator");
                startupLogger.LogInformation("Existing admin user added to Administrator role.");
            }
        }
    }
    catch (Exception ex)
    {
        startupLogger.LogError(ex, "Database initialization error.");
    }
}


if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

app.Run();