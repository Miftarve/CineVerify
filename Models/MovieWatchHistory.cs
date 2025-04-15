using System;
using System.ComponentModel.DataAnnotations;

namespace CineVerify.Models
{
    public class MovieWatchHistory
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public int MovieId { get; set; }

        public DateTime WatchedAt { get; set; } = DateTime.UtcNow;

        public bool Completed { get; set; }

        // Proprietà di navigazione
        public virtual Movie Movie { get; set; } = null!;
        public virtual ApplicationUser User { get; set; } = null!;
    }
}