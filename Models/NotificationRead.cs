using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models
{
    public class NotificationRead
    {
        public int Id { get; set; }

        public int NotificationId { get; set; }
        public Notification Notification { get; set; } = null!;

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        public DateTime ReadAtUtc { get; set; } = DateTime.UtcNow;
    }
}
