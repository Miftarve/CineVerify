using CineVerify.Data;
using CineVerify.Models;
using CineVerify.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CineVerify.Pages.Movies
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly MovieApiService _movieApiService;

        public IndexModel(ApplicationDbContext context, MovieApiService movieApiService)
        {
            _context = context;
            _movieApiService = movieApiService;
        }

        public List<Movie> Movies { get; set; } = new List<Movie>();
        public string SearchQuery { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int PageSize { get; set; } = 12;

        public async Task OnGetAsync(string searchQuery = "", int p = 1)
        {
            SearchQuery = searchQuery;
            CurrentPage = p < 1 ? 1 : p;

            var query = _context.Movies.AsQueryable();

            if (!string.IsNullOrEmpty(SearchQuery))
            {
                // Ricerca per titolo
                query = query.Where(m => m.Title.Contains(SearchQuery) ||
                                         m.OriginalTitle.Contains(SearchQuery));
            }

            // Calcola il numero totale di pagine
            var totalCount = await query.CountAsync();
            TotalPages = (totalCount + PageSize - 1) / PageSize;

            // Imposta il numero di pagina corrente nei limiti consentiti
            if (CurrentPage > TotalPages && TotalPages > 0)
            {
                CurrentPage = TotalPages;
            }

            // Ottieni i film per la pagina corrente
            Movies = await query
                .OrderByDescending(m => m.DateAdded)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            // Se non ci sono film nel database e stiamo cercando, prova a cercare tramite API
            if (Movies.Count == 0 && !string.IsNullOrEmpty(SearchQuery))
            {
                var apiMovies = await _movieApiService.SearchMoviesAsync(SearchQuery);

                // Salva i risultati nel database
                foreach (var movie in apiMovies)
                {
                    // Verifica se il film esiste già nel database
                    var existingMovie = await _context.Movies
                        .FirstOrDefaultAsync(m => m.TmdbId == movie.TmdbId);

                    if (existingMovie == null)
                    {
                        _context.Movies.Add(movie);
                    }
                }

                await _context.SaveChangesAsync();

                // Riprova la query per includere i nuovi film
                Movies = await query
                    .OrderByDescending(m => m.DateAdded)
                    .Skip((CurrentPage - 1) * PageSize)
                    .Take(PageSize)
                    .ToListAsync();
            }
        }
    }
}