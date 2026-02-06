using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceApp.Models
{
    public class Cart
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public List<DbCartItem> Items { get; set; } = new();
    }
}
