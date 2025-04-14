using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;

namespace CineVerify.Models
{
    public class ApplicationUser : IdentityUser
    {
        // Usiamo direttamente i nomi in italiano per compatibilità con il codice esistente
        public string Nome { get; set; }
        public string Cognome { get; set; }
        public DateTime DataRegistrazione { get; set; } = DateTime.UtcNow;

        // Proprietà aggiuntive in inglese
        public string ProfilePictureUrl { get; set; }
        public bool IsCritic { get; set; } = false;

        // Proprietà di compatibilità per codice inglese
        public string FirstName { get => Nome; set => Nome = value; }
        public string LastName { get => Cognome; set => Cognome = value; }
        public DateTime JoinDate { get => DataRegistrazione; set => DataRegistrazione = value; }

        // Relazioni
        public virtual ICollection<MovieReview> Reviews { get; set; }
        public virtual ICollection<MovieUserRating> Ratings { get; set; }
        public virtual ICollection<UserFavorite> Favorites { get; set; }
        public virtual ICollection<MovieWatchHistory> WatchHistory { get; set; }
    }
}