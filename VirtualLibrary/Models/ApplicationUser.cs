using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace VirtualLibrary.Models
{
    public class ApplicationUser
    {
        public int Id { get; set; }

        [ForeignKey("IdentityUser")]
        public string IdentityUserId { get; set; }

        public ICollection<Favorite> Favorites { get; set; }
        public ICollection<Order> Orders { get; set; }
    }
}
