using CineVerify.Data;
using CineVerify.Models;
using CineVerify.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CineVerify.Pages.Admin
{
    [Authorize(Policy = "AdminPolicy")]
    public class ImportMovieModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly MovieApiService _movieApiService;
        private readonly GeminiService _geminiService;

        public ImportMovieModel(
            ApplicationDbContext context,
            MovieApiService movieApiService,
            GeminiService geminiService)
        {
            _context = context;
            _movieApiService = movieApiService;
            _geminiService = geminiService;
        }

        [BindProperty(SupportsGet = true)]
        public string SearchQuery { get; set; } = string.Empty;

        public List<Movie> SearchResults { get; set; } = new List<Movie>();

        public HashSet<int> ExistingMovieIds { get; set; } = new HashSet<int>();

        public Dictionary<int, int> ExistingMovieMap { get; set; } = new Dictionary<int, int>();

        [TempData]
        public string StatusMessage { get; set; } = string.Empty;

        public async Task OnGetAsync()
        {
            if (!string.IsNullOrEmpty(SearchQuery))
            {
                // Esegui la ricerca tramite API
                SearchResults = await _movieApiService.SearchMoviesAsync(SearchQuery);

                // Estrai gli ID TMDB dai risultati
                var tmdbIds = SearchResults.Select(m => m.TmdbId).ToList();

                // Verifica quali film esistono già nel database
                var existingMovies = await _context.Movies
                    .Where(m => tmdbIds.Contains(m.TmdbId))
                    .Select(m => new { m.Id, m.TmdbId })
                    .ToListAsync();

                ExistingMovieIds = existingMovies.Select(m => m.TmdbId).ToHashSet();
                ExistingMovieMap = existingMovies.ToDictionary(m => m.TmdbId, m => m.Id);
            }
        }

        public async Task<IActionResult> OnPostAsync(int tmdbId)
        {
            // Verifica se il film esiste già
            var existingMovie = await _context.Movies.FirstOrDefaultAsync(m => m.TmdbId == tmdbId);
            if (existingMovie != null)
            {
                StatusMessage = $"Il film è già presente nel database.";
                return RedirectToPage("./ImportMovie", new { searchQuery = SearchQuery });
            }

            try
            {
                // Ottieni i dettagli completi dal TMDB
                var movieDetails = await _movieApiService.GetMovieDetailsAsync(tmdbId);

                if (movieDetails == null)
                {
                    StatusMessage = "Errore: Non è stato possibile ottenere i dettagli del film.";
                    return RedirectToPage("./ImportMovie", new { searchQuery = SearchQuery });
                }

                // Imposta il film come non verificato per default
                movieDetails.IsVerified = false;

                // Salva il film nel database
                _context.Movies.Add(movieDetails);
                await _context.SaveChangesAsync();

                // Genera l'analisi in background
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Usa direttamente l'array di generi dal modello
                        string[] genresArray = movieDetails.Genres ?? Array.Empty<string>();

                        // Estrai l'anno dalla data di uscita
                        string releaseYear = movieDetails.ReleaseDate?.Year.ToString() ?? "N/A";

                        // Genera l'analisi con Gemini
                        var analysis = await _geminiService.GenerateMovieAnalysisAsync(
                            movieDetails.Title,
                            movieDetails.Description,
                            genresArray,  // Ora passiamo l'array di generi
                            releaseYear   // E l'anno come stringa
                        );

                        // Aggiorna il film con l'analisi generata
                        var movie = await _context.Movies.FindAsync(movieDetails.Id);
                        if (movie != null)
                        {
                            movie.GeminiAnalysis = analysis;
                            await _context.SaveChangesAsync();
                        }
                    }
                    catch (Exception)
                    {
                        // Errori nell'analisi vengono ignorati
                    }
                });

                StatusMessage = $"Film \"{movieDetails.Title}\" importato con successo.";
                return RedirectToPage("./ImportMovie", new { searchQuery = SearchQuery });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Errore durante l'importazione: {ex.Message}";
                return RedirectToPage("./ImportMovie", new { searchQuery = SearchQuery });
            }
        }
    }
}