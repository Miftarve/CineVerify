using CineVerify.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

namespace CineVerify.Services
{
    public class MovieApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _baseUrl = "https://api.themoviedb.org/3";
        private readonly string _imageBaseUrl = "https://image.tmdb.org/t/p/";

        public MovieApiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["TMDb:ApiKey"];
        }

        public async Task<List<Movie>> SearchMoviesAsync(string query)
        {
            var url = $"{_baseUrl}/search/movie?api_key={_apiKey}&query={Uri.EscapeDataString(query)}&language=it-IT";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var searchResult = JsonSerializer.Deserialize<TmdbSearchResult>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (searchResult?.Results == null)
                return new List<Movie>();

            return searchResult.Results.Select(r => new Movie
            {
                TmdbId = r.Id,
                Title = r.Title,
                OriginalTitle = r.OriginalTitle,
                Description = r.Overview,
                ReleaseDate = ParseDate(r.ReleaseDate),
                PosterPath = !string.IsNullOrEmpty(r.PosterPath) ? $"{_imageBaseUrl}w500{r.PosterPath}" : "",
                BackdropPath = !string.IsNullOrEmpty(r.BackdropPath) ? $"{_imageBaseUrl}original{r.BackdropPath}" : "",
                Rating = (decimal)r.VoteAverage,
                VoteCount = r.VoteCount,
                Genres = Array.Empty<string>(),
                ImdbId = "", // Inizializza ImdbId vuoto
                DateAdded = DateTime.UtcNow // Imposta la data corrente
            }).ToList();
        }

        public async Task<Movie> GetMovieDetailsAsync(int tmdbId)
        {
            var url = $"{_baseUrl}/movie/{tmdbId}?api_key={_apiKey}&language=it-IT&append_to_response=videos,credits";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var details = JsonSerializer.Deserialize<TmdbMovieDetails>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (details == null)
                return null;

            var movie = new Movie
            {
                TmdbId = details.Id,
                Title = details.Title,
                OriginalTitle = details.OriginalTitle,
                Description = details.Overview,
                ReleaseDate = ParseDate(details.ReleaseDate),
                PosterPath = !string.IsNullOrEmpty(details.PosterPath) ? $"{_imageBaseUrl}w500{details.PosterPath}" : "",
                BackdropPath = !string.IsNullOrEmpty(details.BackdropPath) ? $"{_imageBaseUrl}original{details.BackdropPath}" : "",
                Rating = (decimal)details.VoteAverage,
                VoteCount = details.VoteCount,
                Genres = details.Genres?.Select(g => g.Name).ToArray() ?? Array.Empty<string>(),
                ImdbId = details.ImdbId ?? "", // Usa ImdbId dai dettagli TMDB
                DateAdded = DateTime.UtcNow // Imposta la data corrente
            };


            // Aggiungi il trailer se disponibile
            if (details.Videos?.Results != null && details.Videos.Results.Count > 0)
            {
                var trailer = details.Videos.Results
                    .FirstOrDefault(v => v.Type.Equals("Trailer", StringComparison.OrdinalIgnoreCase) &&
                                        v.Site.Equals("YouTube", StringComparison.OrdinalIgnoreCase));

                if (trailer != null)
                {
                    movie.TrailerUrl = trailer.Key;
                }
            }

            return movie;
        }

        private DateTime? ParseDate(string dateString)
        {
            if (string.IsNullOrEmpty(dateString))
                return null;

            if (DateTime.TryParse(dateString, out DateTime result))
                return result;

            return null;
        }

        // Classes for deserializing TMDB API responses
        private class TmdbSearchResult
        {
            public List<TmdbMovie> Results { get; set; }
        }

        private class TmdbMovie
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string OriginalTitle { get; set; }
            public string Overview { get; set; }
            public string ReleaseDate { get; set; }
            public string PosterPath { get; set; }
            public string BackdropPath { get; set; }
            public double VoteAverage { get; set; }
            public int VoteCount { get; set; }
        }

        private class TmdbMovieDetails : TmdbMovie
        {
            public List<TmdbGenre> Genres { get; set; }
            public TmdbVideos Videos { get; set; }
            public string ImdbId { get; set; }
        }

        private class TmdbGenre
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        private class TmdbVideos
        {
            public List<TmdbVideo> Results { get; set; }
        }

        private class TmdbVideo
        {
            public string Key { get; set; }
            public string Site { get; set; }
            public string Type { get; set; }
        }
    }
}