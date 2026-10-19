using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using BlazorQuiz.Components;
using BlazorQuiz.Components.Account;
using BlazorQuiz.Data;
using MudBlazor.Services;
using BlazorQuiz.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// Add Blazor Server support
builder.Services.AddServerSideBlazor();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=quiz.db";

// Pooled factory (singleton): reuses DbContext instances instead of building new options/contexts per call.
// Quiz services, the log writer and the singleton event bus all use it.
builder.Services.AddPooledDbContextFactory<ApplicationDbContext>(options =>
{
    if (connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase) ||
        (connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) && !connectionString.Contains("Server=")))
    {
        var dbSource = connectionString;
        if (connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            var rawPath = connectionString.Substring("Data Source=".Length).Trim().Trim(';');
            if (!Path.IsPathRooted(rawPath))
            {
                var fullPath = Path.Combine(builder.Environment.ContentRootPath, rawPath);
                dbSource = $"Data Source={fullPath}";
            }
        }
        options.UseSqlite(dbSource);
    }
    else
    {
        options.UseSqlServer(connectionString);
    }
});

// Identity needs a scoped ApplicationDbContext: hand out a pooled one per scope
builder.Services.AddScoped(provider =>
    provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Update Identity configuration to include roles
builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false) // Changed to false for easier access
    .AddRoles<IdentityRole>() // Add roles support
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

// In-process event bus (replaces the SignalR hub loopback)
builder.Services.AddSingleton<IQuizEventBus, QuizEventBus>();

// Add HttpContextAccessor for ActivityLogService
builder.Services.AddHttpContextAccessor();

// UI language (Indonesian / English), chosen through the ui-lang cookie
builder.Services.AddScoped<ILocalizer, Localizer>();

// Add TimeZone service for Indonesian time (GMT+7)
builder.Services.AddSingleton<ITimeZoneService, TimeZoneService>();

// Add QuizService (now using DbContextFactory for thread safety)
builder.Services.AddScoped<IQuizService, QuizService>(provider =>
{
    var contextFactory = provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
    var timeZoneService = provider.GetRequiredService<ITimeZoneService>();
    var memoryCache = provider.GetRequiredService<IMemoryCache>();
    var eventBus = provider.GetRequiredService<IQuizEventBus>();
    return new QuizService(contextFactory, timeZoneService, memoryCache, eventBus);
});

// Add ActivityLogService (now uses Indonesian timezone)
builder.Services.AddSingleton<ActivityLogQueue>();
builder.Services.AddHostedService<ActivityLogWriter>();
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();

// Add DailyResetService (resets demo data daily at 00:00 WIB)
builder.Services.AddHostedService<DailyResetService>();

// Add MemoryCache for caching
builder.Services.AddMemoryCache();

var app = builder.Build();

// Ensure database schema and seed default users
using (var scope = app.Services.CreateScope())
{
    try
    {
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using (var dbContext = contextFactory.CreateDbContext())
        {
            await dbContext.Database.EnsureCreatedAsync();
            await DailyResetService.EnsureDefaultQuizAsync(dbContext);
        }

        await DataSeeder.SeedDefaultUserAsync(scope.ServiceProvider);

        // Optional: Log timezone info for debugging (remove in production)
        if (app.Environment.IsDevelopment())
        {
            var timeZoneService = scope.ServiceProvider.GetRequiredService<ITimeZoneService>();
            timeZoneService.LogTimeZoneInfo();
        }
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding default users.");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Language switch: stores the choice in a cookie and returns to the page the user came from.
app.MapGet("/set-language", (string lang, string? returnUrl, HttpContext context) =>
{
    if (Localizer.IsSupported(lang))
    {
        context.Response.Cookies.Append(Localizer.CookieName, lang, new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });
    }

    var target = !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//") && !returnUrl.StartsWith("/\\")
        ? returnUrl
        : "/";
    return Results.LocalRedirect(target);
}).AllowAnonymous();

// Auto-login for Host Quiz and Editor Quiz in demo mode
app.MapGet("/auto-login", async (
    string? target,
    IDbContextFactory<ApplicationDbContext> contextFactory,
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IConfiguration config,
    ILogger<Program> logger) =>
{
    try
    {
        // Guarantee database and tables are created before accessing Identity
        await using (var db = contextFactory.CreateDbContext())
        {
            await db.Database.EnsureCreatedAsync();
            await DailyResetService.EnsureDefaultQuizAsync(db);
        }

        var adminEmail = config["Seed:AdminEmail"] ?? "admin@blazorquiz.com";
        var adminPassword = config["Seed:AdminPassword"] ?? "Admin123!";

        // Ensure roles exist
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }
        if (!await roleManager.RoleExistsAsync("User"))
        {
            await roleManager.CreateAsync(new IdentityRole("User"));
        }

        // Find or create admin user
        var user = await userManager.FindByEmailAsync(adminEmail)
                   ?? await userManager.FindByNameAsync(adminEmail);

        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };
            var createResult = await userManager.CreateAsync(user, adminPassword);
            if (!createResult.Succeeded)
            {
                logger.LogError("Gagal membuat user admin demo: {Errors}",
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                return Results.Problem($"Gagal membuat user demo: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            }
        }

        if (!await userManager.IsInRoleAsync(user, "Admin"))
        {
            await userManager.AddToRoleAsync(user, "Admin");
        }

        await signInManager.SignInAsync(user, isPersistent: true);

        var destination = target?.ToLowerInvariant() switch
        {
            "host" => "/quiz-host",
            "editor" => "/quiz-editor",
            _ => "/"
        };

        return Results.Redirect(destination);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error saat auto-login demo.");
        return Results.Problem($"Auto-login gagal: {ex.Message}");
    }
}).AllowAnonymous();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();
