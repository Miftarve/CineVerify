using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CineVerify.Models
{
    public class Movie
    {
        public int Id { get; set; }

        public int TmdbId { get; set; }

        public string ImdbId { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string OriginalTitle { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public DateTime? ReleaseDate { get; set; }

        public DateTime DateAdded { get; set; } = DateTime.UtcNow;

        // Nota: non usiamo Column(TypeName="decimal(3,1)") in quanto SQLite lo supporta in modo diverso
        public decimal Rating { get; set; }

        public int VoteCount { get; set; }

        public string PosterPath { get; set; } = string.Empty;

        public string BackdropPath { get; set; } = string.Empty;

        public string TrailerUrl { get; set; } = string.Empty;

        public bool IsVerified { get; set; }

        public string[] Genres { get; set; } = Array.Empty<string>();

        public string GeminiAnalysis { get; set; } = string.Empty;

        // Proprietà di supporto per verificare se le immagini sono disponibili
        [NotMapped]
        public bool HasPoster => !string.IsNullOrEmpty(PosterPath);

        [NotMapped]
        public bool HasBackdrop => !string.IsNullOrEmpty(BackdropPath);

        [NotMapped]
        public bool HasTrailer => !string.IsNullOrEmpty(TrailerUrl);
    }
}