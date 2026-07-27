
using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? IconKey { get; set; }

        [MaxLength(100)]
        [RegularExpression(@"^[a-zA-Z0-9 _-]+$")]
        public string? IconClass { get; set; }

        [MaxLength(20)]
        [RegularExpression(@"^#[0-9a-fA-F]{6}$")]
        public string? IconColor { get; set; }

        [MaxLength(20)]
        [RegularExpression(@"^#[0-9a-fA-F]{6}$")]
        public string? IconBgColor { get; set; }

        public bool IsCore { get; set; }

        public ICollection<Product> Products { get; set; } = new List<Product>();

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];
    }
}
