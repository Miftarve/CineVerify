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

namespace CineVerify.Pages.Movies
{
    [Authorize]
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly MovieApiService _movieApiService;
        private readonly GeminiService _geminiService;
        private readonly UserManager<ApplicationUser> _userManager;

        public DetailsModel(
            ApplicationDbContext context,
            MovieApiService movieApiService,
            GeminiService geminiService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _movieApiService = movieApiService;
            _geminiService = geminiService;
            _userManager = userManager;
        }

        public Movie? Movie { get; set; }
        public List<MovieReview> Reviews { get; set; } = new List<MovieReview>();
        public MovieUserRating? UserRating { get; set; }
        // Aggiunto per ottenere il valore numerico del rating
        public decimal UserRatingValue => UserRating?.Rating ?? 0;
        public MovieReview? UserReview { get; set; }
        public bool IsFavorite { get; set; }

        private void FixImagePaths(Movie movie)
        {
            // Base URL per le immagini di TMDB
            const string imageBaseUrl = "https://image.tmdb.org/t/p/";

            // Correggi il percorso del poster
            if (!string.IsNullOrEmpty(movie.PosterPath) && !movie.PosterPath.StartsWith("http"))
            {
                movie.PosterPath = $"{imageBaseUrl}w500{movie.PosterPath}";
            }

            // Correggi il percorso dell'immagine di sfondo
            if (!string.IsNullOrEmpty(movie.BackdropPath) && !movie.BackdropPath.StartsWith("http"))
            {
                movie.BackdropPath = $"{imageBaseUrl}original{movie.BackdropPath}";
            }
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            // Carica il film dal database
            Movie = await _context.Movies.FindAsync(id);

            if (Movie == null)
            {
                return NotFound();
            }

            FixImagePaths(Movie);

            // Carica recensioni
            Reviews = await _context.MovieReviews
                .Include(r => r.User)
                .Where(r => r.MovieId == id && r.IsApproved)
                .OrderByDescending(r => r.DateCreated)
                .ToListAsync();

            // Carica valutazione dell'utente
            UserRating = await _context.MovieUserRatings
                .FirstOrDefaultAsync(r => r.MovieId == id && r.UserId == currentUser.Id);

            // Carica recensione dell'utente
            UserReview = await _context.MovieReviews
                .FirstOrDefaultAsync(r => r.MovieId == id && r.UserId == currentUser.Id);

            // Controlla se il film è nei preferiti dell'utente
            IsFavorite = await _context.UserFavorites
                .AnyAsync(f => f.MovieId == id && f.UserId == currentUser.Id);

            // Aggiungi una visualizzazione alla cronologia
            var history = new MovieWatchHistory
            {
                UserId = currentUser.Id,
                MovieId = id
            };
            _context.MovieWatchHistory.Add(history);

            // Se il film non ha un'analisi Gemini, generala
            if (string.IsNullOrEmpty(Movie.GeminiAnalysis))
            {
                // Genera l'analisi con Gemini in background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var analysis = await _geminiService.GenerateMovieAnalysisAsync(
                            Movie.Title,
                            Movie.Description,
                            Movie.Genres,
                            Movie.ReleaseDate?.Year.ToString() ?? "N/A"
                        );

                        Movie.GeminiAnalysis = analysis;
                        await _context.SaveChangesAsync();
                    }
                    catch
                    {
                        // Ignora errori nell'analisi - sarà ritentata al prossimo caricamento
                    }
                });
            }

            await _context.SaveChangesAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostAddToFavoritesAsync(int movieId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            // Verifica che il film esista
            var movie = await _context.Movies.FindAsync(movieId);
            if (movie == null)
            {
                TempData["ErrorMessage"] = "Il film richiesto non è stato trovato.";
                return RedirectToPage("/Index");
            }

            // Crea il nuovo preferito
            var newFavorite = new UserFavorite
            {
                UserId = currentUser.Id,
                MovieId = movieId,
                DateAdded = DateTime.UtcNow
            };

            _context.UserFavorites.Add(newFavorite);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Details", new { id = movieId });
        }

        public async Task<IActionResult> OnPostRemoveFromFavoritesAsync(int movieId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            // Verifica che il film esista
            var movie = await _context.Movies.FindAsync(movieId);
            if (movie == null)
            {
                TempData["ErrorMessage"] = "Il film richiesto non è stato trovato.";
                return RedirectToPage("/Index");
            }

            // Trova il preferito
            var favorite = await _context.UserFavorites
                .FirstOrDefaultAsync(f => f.MovieId == movieId && f.UserId == currentUser.Id);

            if (favorite != null)
            {
                _context.UserFavorites.Remove(favorite);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Details", new { id = movieId });
        }

        public async Task<IActionResult> OnPostAddReviewAsync(int movieId, string title, string content, decimal rating)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            // Verifica se l'utente ha già recensito questo film
            var existingReview = await _context.MovieReviews
                .FirstOrDefaultAsync(r => r.MovieId == movieId && r.UserId == currentUser.Id);

            if (existingReview != null)
            {
                // Aggiorna la recensione esistente
                existingReview.Title = title;
                existingReview.Content = content;
                existingReview.Rating = rating;
                existingReview.DateModified = DateTime.UtcNow;
            }
            else
            {
                // Crea una nuova recensione
                var review = new MovieReview
                {
                    UserId = currentUser.Id,
                    UserName = currentUser.UserName,
                    MovieId = movieId,
                    Title = title,
                    Content = content,
                    Rating = rating,
                    DateCreated = DateTime.UtcNow,
                    IsApproved = true // Se vuoi un processo di approvazione, imposta a false
                };

                _context.MovieReviews.Add(review);
            }

            await _context.SaveChangesAsync();

            // Analisi del sentiment con Gemini in background
            _ = Task.Run(async () =>
            {
                try
                {
                    var review = await _context.MovieReviews
                        .FirstOrDefaultAsync(r => r.MovieId == movieId && r.UserId == currentUser.Id);

                    if (review != null)
                    {
                        review.SentimentAnalysis = await _geminiService.AnalyzeSentimentAsync(content);
                        await _context.SaveChangesAsync();
                    }
                }
                catch
                {
                    // Ignora errori nell'analisi
                }
            });

            return RedirectToPage("./Details", new { id = movieId });
        }

        public async Task<IActionResult> OnPostEditReviewAsync(int reviewId, int movieId, string content, decimal rating)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            // Trova la recensione
            var review = await _context.MovieReviews
                .FirstOrDefaultAsync(r => r.Id == reviewId && r.UserId == currentUser.Id);

            if (review == null)
            {
                return NotFound();
            }

            // Aggiorna la recensione
            review.Content = content;
            review.Rating = rating;
            review.DateModified = DateTime.UtcNow;

            // Resetta l'analisi del sentiment poiché il contenuto è cambiato
            review.SentimentAnalysis = null;

            await _context.SaveChangesAsync();

            // Genera una nuova analisi del sentiment
            _ = Task.Run(async () =>
            {
                try
                {
                    review.SentimentAnalysis = await _geminiService.AnalyzeSentimentAsync(content);
                    await _context.SaveChangesAsync();
                }
                catch
                {
                    // Ignora errori nell'analisi
                }
            });

            return RedirectToPage("./Details", new { id = movieId });
        }

        public async Task<IActionResult> OnPostDeleteReviewAsync(int reviewId, int movieId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            // Trova la recensione
            var review = await _context.MovieReviews
                .FirstOrDefaultAsync(r => r.Id == reviewId && (r.UserId == currentUser.Id || User.IsInRole("Admin")));

            if (review == null)
            {
                return NotFound();
            }

            // Elimina la recensione
            _context.MovieReviews.Remove(review);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Details", new { id = movieId });
        }

        public async Task<IActionResult> OnPostAddToWatchlistAsync(int movieId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return Challenge();
            }

            var movie = await _context.Movies.FindAsync(movieId);
            if (movie == null)
            {
                return NotFound();
            }

            var watchRecord = new MovieWatchHistory
            {
                UserId = currentUser.Id,
                MovieId = movieId,
                WatchedAt = DateTime.UtcNow
            };

            _context.MovieWatchHistory.Add(watchRecord);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Details", new { id = movieId });
        }

        public async Task<IActionResult> OnPostGenerateAnalysisAsync(int movieId)
        {
            if (!User.IsInRole("Admin") && !User.IsInRole("Critic"))
            {
                return Forbid();
            }

            var movie = await _context.Movies.FindAsync(movieId);
            if (movie == null)
            {
                return NotFound();
            }

            try
            {
                movie.GeminiAnalysis = await _geminiService.GenerateMovieAnalysisAsync(
                    movie.Title,
                    movie.Description,
                    movie.Genres,
                    movie.ReleaseDate?.Year.ToString() ?? "N/A"
                );

                await _context.SaveChangesAsync();
            }
            catch
            {
                // Gestione errori
            }

            return RedirectToPage("./Details", new { id = movieId });
        }
    }
}