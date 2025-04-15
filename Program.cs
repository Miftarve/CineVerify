using CineVerify.Data;
using CineVerify.Models;
using CineVerify.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddRazorPages();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminPolicy", policy =>
        policy.RequireRole("Admin"));
    options.AddPolicy("ModeratorPolicy", policy =>
        policy.RequireRole("Moderator", "Admin"));
});

// Registrazione base di HttpClient
builder.Services.AddHttpClient();

// Servizi custom
builder.Services.AddScoped<GeminiService>();
builder.Services.AddHttpClient<GeminiService>(client =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Servizio API per film
builder.Services.AddScoped<MovieApiService>();

builder.Services.AddHttpClient<MovieApiService>(client =>
{
    client.BaseAddress = new Uri("https://api.themoviedb.org/3/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

var app = builder.Build();

// Inizializzazione del database e importazione film
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var movieApiService = services.GetRequiredService<MovieApiService>();

        // Inizializza il database (crea le tabelle se non esistono)
        context.Database.EnsureCreated();

        // Aggiungi ruoli se non esistono
        await CreateRolesAsync(roleManager);

        // Aggiungi dati di esempio
        await DbInitializer.InitializeAsync(context);

        // Aggiungi un utente amministratore se non esiste
        await CreateAdminUserAsync(userManager);

        // Conta i film esistenti per vedere se dobbiamo importarne di più
        var existingMoviesCount = await context.Movies.CountAsync();

        // Se ci sono pochi film, importa film popolari da TMDB
        if (existingMoviesCount < 10)
        {
            logger.LogInformation("Importazione film popolari dall'API TMDB...");

            try
            {
                // Importa film popolari (3 pagine = 60 film max)
                var importedMovies = await movieApiService.ImportMultiplePagesOfMoviesAsync(3);
                logger.LogInformation($"Importati con successo {importedMovies.Count} film popolari");

                // Importa alcuni film per genere (i più comuni)
                var genreIds = new[] { 28, 12, 35, 18, 27, 10749, 878 }; // Action, Adventure, Comedy, Drama, Horror, Romance, Sci-Fi
                foreach (var genreId in genreIds)
                {
                    try
                    {
                        var importedGenre = await movieApiService.ImportMoviesByGenreAsync(genreId, 1);
                        logger.LogInformation($"Importati {importedGenre.Count} film per il genere {genreId}");
                    }
                    catch (Exception genreEx)
                    {
                        logger.LogWarning(genreEx, $"Errore durante l'importazione di film del genere {genreId}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Errore durante l'importazione automatica dei film.");
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Si è verificato un errore durante l'inizializzazione del DB.");
    }
}

// Configure the HTTP request pipeline.
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

app.Run();

// Metodi di supporto per l'inizializzazione
async Task CreateRolesAsync(RoleManager<IdentityRole> roleManager)
{
    string[] roleNames = { "Admin", "Moderator", "User" };
    foreach (var roleName in roleNames)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }
}

async Task CreateAdminUserAsync(UserManager<ApplicationUser> userManager)
{
    var adminEmail = "admin@cineverify.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);

    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            Nome = "Admin",
            Cognome = "CineVerify",
            DataRegistrazione = DateTime.UtcNow,
            EmailConfirmed = true,
            ProfilePictureUrl = "/images/default-avatar.png"
        };

        var result = await userManager.CreateAsync(adminUser, "Admin123!");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
}