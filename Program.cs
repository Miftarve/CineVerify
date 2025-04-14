using CineVerify.Data;
using CineVerify.Models;
using CineVerify.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Configurazione dei servizi
ConfigureServices(builder);

var app = builder.Build();

// 2. Configurazione della pipeline HTTP
ConfigureMiddleware(app);

// 3. Seed dei dati iniziali
await SeedDatabase(app);

// 4. Configurazione delle route
ConfigureRoutes(app);

app.Run();

void ConfigureServices(WebApplicationBuilder builder)
{
    // Connessione al database
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(connectionString));

    // Filtro per eccezioni del database
    builder.Services.AddDatabaseDeveloperPageExceptionFilter();

    // Configurazione di Identity
    builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddDefaultUI();

    // Configurazione dei cookie
    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.LoginPath = "/Login";
        options.LogoutPath = "/Logout";
        options.AccessDeniedPath = "/AccessDenied";
    });

    // Configurazione di Razor Pages
    builder.Services.AddRazorPages(options =>
    {
        options.Conventions.AuthorizeFolder("/Admin", "AdminPolicy");
    });

    // Configurazione delle autorizzazioni
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("AdminPolicy", policy => policy.RequireRole("Admin"));
    });

    // Registrazione dei servizi personalizzati
    builder.Services.AddHttpClient<GeminiService>();
    builder.Services.AddScoped<MovieApiService>();
    builder.Services.AddScoped<GeminiService>();
}

void ConfigureMiddleware(WebApplication app)
{
    // Configurazione ambiente di sviluppo
    if (app.Environment.IsDevelopment())
    {
        app.UseMigrationsEndPoint();
    }
    else
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    // Middleware standard
    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();
}

async Task SeedDatabase(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;

    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        // Assicurati che il database esista e applica le migrazioni
        context.Database.EnsureCreated();
        context.Database.Migrate();

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        // Crea i ruoli se non esistono
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        if (!await roleManager.RoleExistsAsync("User"))
        {
            await roleManager.CreateAsync(new IdentityRole("User"));
        }

        // Crea un admin predefinito se non esiste
        var adminUser = await userManager.FindByEmailAsync("admin@cineverify.com");
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = "admin@cineverify.com",
                Email = "admin@cineverify.com",
                Nome = "Admin",
                Cognome = "Sistema",
                EmailConfirmed = true
            };

            await userManager.CreateAsync(adminUser, "Admin123!");
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Si è verificato un errore durante il seeding del database.");
    }
}

void ConfigureRoutes(WebApplication app)
{
    // Configurazione delle route
    app.MapRazorPages();

    app.MapGet("/", context =>
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            context.Response.Redirect("/Index");
        }
        else
        {
            context.Response.Redirect("/Login");
        }
        return Task.CompletedTask;
    });
}