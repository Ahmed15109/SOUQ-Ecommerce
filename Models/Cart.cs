using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models
{
    public class Cart
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<DbCartItem> Items { get; set; } = new();
    }
}
