using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceApp.Models
{
    public class Notification
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(1000)]
        public string Message { get; set; } = string.Empty;

        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        public bool IsForAdmin { get; set; }

        public bool IsRead { get; set; } = false;

        public int? OrderId { get; set; }
        public Order? Order { get; set; }

        public int? PharmacyRequestId { get; set; }
        public PharmacyRequest? PharmacyRequest { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<NotificationRead> Reads { get; set; } = new List<NotificationRead>();
    }
}
