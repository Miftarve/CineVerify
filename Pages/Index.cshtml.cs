using CineVerify.Data;
using CineVerify.Models;
using CineVerify.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CineVerify.Pages
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly GeminiService _geminiService;

        public IndexModel(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            GeminiService geminiService)
        {
            _context = context;
            _userManager = userManager;
            _geminiService = geminiService;
        }

        public List<Movie> LatestMovies { get; set; } = new List<Movie>();
        public List<Movie> TopRatedMovies { get; set; } = new List<Movie>();
        public List<Movie> RecommendedMovies { get; set; } = new List<Movie>();

        public string RecommendationText { get; set; } = string.Empty;
        public bool HasRecommendations { get; set; } = false;

        public async Task OnGetAsync()
        {
            // Ottieni i film più recenti
            LatestMovies = await _context.Movies
                .OrderByDescending(m => m.ReleaseDate)
                .Take(4)
                .ToListAsync();

            // Ottieni i film con valutazione più alta
            TopRatedMovies = await _context.Movies
                .Where(m => m.VoteCount > 5) // Solo film con abbastanza voti
                .OrderByDescending(m => m.Rating)
                .Take(4)
                .ToListAsync();

            // Per le raccomandazioni personalizzate
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null)
            {
                // Ottieni film che l'utente potrebbe apprezzare basandosi sui suoi preferiti e valutazioni
                var favoriteMovies = await _context.UserFavorites
                    .Where(f => f.UserId == currentUser.Id)
                    .Include(f => f.Movie)
                    .Select(f => f.Movie)
                    .ToListAsync();

                var userRatings = await _context.MovieUserRatings
                    .Where(r => r.UserId == currentUser.Id && r.Rating >= 7) // Solo rating positivi
                    .Include(r => r.Movie)
                    .Select(r => r.Movie)
                    .ToListAsync();

                // Raccogli i generi preferiti dell'utente
                var favoriteGenres = new HashSet<string>();

                foreach (var movie in favoriteMovies.Concat(userRatings))
                {
                    if (movie.Genres != null && movie.Genres.Length > 0)
                    {
                        foreach (var genre in movie.Genres)
                        {
                            favoriteGenres.Add(genre);
                        }
                    }
                }

                // Se abbiamo alcuni generi preferiti, cerca film simili
                if (favoriteGenres.Any())
                {
                    // Film che l'utente non ha ancora valutato o aggiunto ai preferiti
                    var ratedMovieIds = userRatings.Select(m => m.Id).ToHashSet();
                    var favoriteMovieIds = favoriteMovies.Select(m => m.Id).ToHashSet();
                    var excludedMovieIds = ratedMovieIds.Union(favoriteMovieIds).ToHashSet();

                    // Trova film con generi simili
                    RecommendedMovies = new List<Movie>();
                    var allMovies = await _context.Movies.ToListAsync();

                    foreach (var movie in allMovies)
                    {
                        if (excludedMovieIds.Contains(movie.Id))
                            continue;

                        if (movie.Genres != null && movie.Genres.Length > 0)
                        {
                            if (movie.Genres.Any(g => favoriteGenres.Contains(g)))
                            {
                                RecommendedMovies.Add(movie);
                                if (RecommendedMovies.Count >= 4)
                                    break;
                            }
                        }
                    }

                    // Se abbiamo raccomandazioni o abbastanza dati dell'utente, chiedi a Gemini di generare consigli personalizzati
                    if ((favoriteMovies.Count + userRatings.Count) >= 3)
                    {
                        HasRecommendations = true;

                        var favoriteMovieTitles = favoriteMovies.Select(m => m.Title).Take(5).ToArray();
                        var ratedMovieTitles = userRatings.Select(m => m.Title).Take(5).ToArray();
                        var userRatingValues = await _context.MovieUserRatings
                            .Where(r => r.UserId == currentUser.Id)
                            .OrderByDescending(r => r.DateRated)
                            .Take(5)
                            .Select(r => (int)r.Rating)
                            .ToArrayAsync();

                        try
                        {
                            // Genera consigli solo se non li abbiamo già
                            var userRecommendationKey = $"user_recommendation_{currentUser.Id}";
                            var recommendationTimestampKey = $"recommendation_timestamp_{currentUser.Id}";

                            // Verifica se c'è una raccomandazione recente (meno di 24 ore)
                            var timestampStr = await _context.UserFavorites
                                .Where(f => f.UserId == userRecommendationKey)
                                .Select(f => f.DateAdded.ToString())
                                .FirstOrDefaultAsync();

                            bool needsNewRecommendation = true;

                            if (!string.IsNullOrEmpty(timestampStr) &&
                                DateTime.TryParse(timestampStr, out DateTime timestamp))
                            {
                                // Se la raccomandazione è stata generata nelle ultime 24 ore, non rigenerarla
                                if ((DateTime.UtcNow - timestamp).TotalHours < 24)
                                {
                                    needsNewRecommendation = false;

                                    // Recupera la raccomandazione salvata
                                    var savedRecommendation = await _context.MovieReviews
                                        .Where(r => r.UserId == userRecommendationKey)
                                        .Select(r => r.Content)
                                        .FirstOrDefaultAsync();

                                    if (!string.IsNullOrEmpty(savedRecommendation))
                                    {
                                        RecommendationText = savedRecommendation;
                                    }
                                    else
                                    {
                                        // Se c'è un timestamp ma non il testo, rigenerare
                                        needsNewRecommendation = true;
                                    }
                                }
                            }

                            if (needsNewRecommendation)
                            {
                                // Genera nuovi consigli
                                RecommendationText = await _geminiService.GeneratePersonalizedRecommendationsAsync(
                                    currentUser.Id,
                                    favoriteGenres.ToArray(),
                                    favoriteMovieTitles.Concat(ratedMovieTitles).Distinct().ToArray(),
                                    userRatingValues
                                );

                                // Salva la raccomandazione
                                var existingRecommendation = await _context.MovieReviews
                                    .FirstOrDefaultAsync(r => r.UserId == userRecommendationKey);

                                if (existingRecommendation != null)
                                {
                                    existingRecommendation.Content = RecommendationText;
                                    existingRecommendation.DateCreated = DateTime.UtcNow;
                                }
                                else
                                {
                                    // Crea un record nella tabella MovieReviews per memorizzare le raccomandazioni
                                    // Usiamo un MovieId = 1 solo come segnaposto
                                    var recommendationRecord = new MovieReview
                                    {
                                        UserId = userRecommendationKey,
                                        MovieId = 1, // Usa l'ID del primo film o crea un film speciale
                                        Content = RecommendationText,
                                        Title = "Consigli Personalizzati",
                                        Rating = 0,
                                        DateCreated = DateTime.UtcNow
                                    };
                                    _context.MovieReviews.Add(recommendationRecord);
                                }

                                // Aggiorna il timestamp
                                var existingTimestamp = await _context.UserFavorites
                                    .FirstOrDefaultAsync(f => f.UserId == userRecommendationKey);

                                if (existingTimestamp != null)
                                {
                                    existingTimestamp.DateAdded = DateTime.UtcNow;
                                }
                                else
                                {
                                    // Crea un record nella tabella UserFavorites per memorizzare il timestamp
                                    // Usiamo un MovieId = 1 solo come segnaposto
                                    var timestampRecord = new UserFavorite
                                    {
                                        UserId = userRecommendationKey,
                                        MovieId = 1, // Usa l'ID del primo film o crea un film speciale
                                        DateAdded = DateTime.UtcNow
                                    };
                                    _context.UserFavorites.Add(timestampRecord);
                                }

                                await _context.SaveChangesAsync();
                            }
                        }
                        catch (Exception)
                        {
                            // Se c'è un errore nel generare consigli, mostriamo solo film simili
                        }
                    }
                }
            }

            // Se non abbiamo trovato raccomandazioni, mostra film casuali
            if (RecommendedMovies.Count == 0)
            {
                var random = new Random();
                var allMovieIds = await _context.Movies.Select(m => m.Id).ToListAsync();
                var randomMovieIds = allMovieIds.OrderBy(x => random.Next()).Take(4).ToList();

                RecommendedMovies = await _context.Movies
                    .Where(m => randomMovieIds.Contains(m.Id))
                    .ToListAsync();
            }
        }
    }
}