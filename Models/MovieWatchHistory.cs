using System;

namespace CineVerify.Models
{
    public class MovieWatchHistory
    {
        public int Id { get; set; }

        public int MovieId { get; set; }
        public virtual Movie Movie { get; set; }

        public string UserId { get; set; }
        public virtual ApplicationUser User { get; set; }

        public DateTime WatchedAt { get; set; } = DateTime.UtcNow;
    }
}