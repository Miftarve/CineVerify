using CineVerify.Data;
using CineVerify.Models;
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
        private readonly int _pageSize = 500; 

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public PaginatedList<Movie> Movies { get; set; } = null!;
        public List<string> AvailableGenres { get; set; } = new List<string>();

        [BindProperty(SupportsGet = true)]
        public string SearchQuery { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string GenreFilter { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(int? pageIndex)
        {
            // 1. Prima carica tutti i film dal database per elaborare i generi
            var allMovies = await _context.Movies.AsNoTracking().ToListAsync();

            // 2. Estrai i generi lato client
            AvailableGenres = allMovies
                .Where(m => m.Genres != null && m.Genres.Length > 0)
                .SelectMany(m => m.Genres)
                .Distinct()
                .OrderBy(g => g)
                .ToList();

            // 3. Query base per i film da visualizzare
            var movieQuery = _context.Movies.AsQueryable();

            // 4. Applica i filtri di base che EF Core può gestire
            if (!string.IsNullOrEmpty(SearchQuery))
            {
                movieQuery = movieQuery.Where(m =>
                    m.Title.Contains(SearchQuery) ||
                    m.OriginalTitle.Contains(SearchQuery) ||
                    m.Description.Contains(SearchQuery));
            }

            // 5. Carica i risultati nel client per applicare filtri complessi
            var filteredMovies = await movieQuery.ToListAsync();

            // 6. Applica il filtro per genere lato client
            if (!string.IsNullOrEmpty(GenreFilter))
            {
                filteredMovies = filteredMovies
                    .Where(m => m.Genres != null && m.Genres.Contains(GenreFilter))
                    .ToList();
            }

            // 7. Ordina i risultati
            filteredMovies = filteredMovies.OrderByDescending(m => m.DateAdded).ToList();

            // 8. Crea una lista paginata manualmente
            int totalItems = filteredMovies.Count;
            pageIndex = pageIndex ?? 1;
            int totalPages = (int)Math.Ceiling(totalItems / (double)_pageSize);

            var paginatedMovies = filteredMovies
                .Skip((pageIndex.Value - 1) * _pageSize)
                .Take(_pageSize)
                .ToList();

            Movies = new PaginatedList<Movie>(paginatedMovies, totalItems, pageIndex.Value, _pageSize);

            return Page();
        }
    }
}