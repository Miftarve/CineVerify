using CineVerify.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace CineVerify.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Movie> Movies { get; set; }
        public DbSet<MovieReview> MovieReviews { get; set; }
        public DbSet<MovieUserRating> MovieUserRatings { get; set; }
        public DbSet<UserFavorite> UserFavorites { get; set; }
        public DbSet<MovieWatchHistory> MovieWatchHistory { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Crea un convertitore personalizzato senza usare l'operatore di propagazione null
            var jsonConverter = new ValueConverter<string[], string>(
                // Converti da array di stringhe a stringa JSON
                v => SerializeGenres(v),
                // Converti da stringa JSON ad array di stringhe con gestione degli errori
                v => SafeJsonDeserialize(v)
            );

            // Crea un confrontatore di valori sicuro
            var valueComparer = new ValueComparer<string[]>(
                (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
                c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v == null ? 0 : v.GetHashCode())),
                c => c == null ? null : c.ToArray()
            );

            // Applica il convertitore e il confrontatore
            modelBuilder.Entity<Movie>()
                .Property(m => m.Genres)
                .HasConversion(jsonConverter)
                .Metadata.SetValueComparer(valueComparer);

            // Configura i tipi di colonne per SQLite
            // SQLite usa REAL per i numeri a virgola mobile
            modelBuilder.Entity<Movie>().Property(m => m.Rating).HasColumnType("REAL");
            modelBuilder.Entity<MovieReview>().Property(r => r.Rating).HasColumnType("REAL");
            modelBuilder.Entity<MovieUserRating>().Property(r => r.Rating).HasColumnType("REAL");

            // Configura la relazione tra MovieReview e ApplicationUser
            modelBuilder.Entity<MovieReview>()
                .HasOne(r => r.User)
                .WithMany(u => u.Reviews)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configura chiavi composte per le tabelle di relazione
            modelBuilder.Entity<MovieUserRating>()
                .HasKey(r => new { r.UserId, r.MovieId });

            modelBuilder.Entity<UserFavorite>()
                .HasKey(f => new { f.UserId, f.MovieId });

            modelBuilder.Entity<MovieWatchHistory>()
                .HasKey(h => new { h.UserId, h.MovieId });

            // Configura le relazioni esplicite per le tabelle Movie
            modelBuilder.Entity<Movie>()
                .HasMany<MovieReview>()
                .WithOne()
                .HasForeignKey(r => r.MovieId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configura la gestione DATE per SQLite
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var dateTimeProperties = entityType.ClrType.GetProperties()
                    .Where(p => p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTime?));

                foreach (var property in dateTimeProperties)
                {
                    // Configura il tipo di colonna TEXT per i DateTime in SQLite
                    modelBuilder
                        .Entity(entityType.Name)
                        .Property(property.Name)
                        .HasColumnType("TEXT");
                }
            }
        }

        // Metodo helper per serializzare in modo sicuro un array di stringhe
        private static string SerializeGenres(string[] value)
        {
            if (value == null)
            {
                return JsonSerializer.Serialize(Array.Empty<string>(), new JsonSerializerOptions());
            }
            return JsonSerializer.Serialize(value, new JsonSerializerOptions());
        }

        // Metodo helper per deserializzare in modo sicuro una stringa JSON
        private static string[] SafeJsonDeserialize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Array.Empty<string>();

            try
            {
                var result = JsonSerializer.Deserialize<string[]>(value, new JsonSerializerOptions());
                if (result == null)
                    return Array.Empty<string>();
                return result;
            }
            catch (JsonException)
            {
                // In caso di errore di deserializzazione, restituisci un array vuoto
                return Array.Empty<string>();
            }
        }
    }
}