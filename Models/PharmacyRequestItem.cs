using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceApp.Models
{
    public class PharmacyRequestItem
    {
        public int Id { get; set; }

        public int PharmacyRequestId { get; set; }
        [ForeignKey("PharmacyRequestId")]
        public PharmacyRequest? PharmacyRequest { get; set; }

        [Required]
        [MaxLength(150)]
        public string MedicineName { get; set; } = string.Empty;

        [Required]
        [Range(1, 100)]
        public int Quantity { get; set; }
    }
}
