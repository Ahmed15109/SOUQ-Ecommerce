using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceApp.Models
{
    public class Address
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        [Required]
        public string City { get; set; } = string.Empty;

        [Required]
        public string Area { get; set; } = string.Empty;

        [Required]
        public string Street { get; set; } = string.Empty;

        [Required]
        public string Building { get; set; } = string.Empty;

        public string Notes { get; set; } = string.Empty;

        public bool IsDefault { get; set; }

        public string FullAddress => $"{City}, {Area}, {Street}, Bld: {Building}";
    }
}
