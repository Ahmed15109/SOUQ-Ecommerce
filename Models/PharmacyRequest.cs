using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models
{
    public class PharmacyRequest
    {
        public int Id { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? UserId { get; set; }

        [MaxLength(20)]
        public string? UserPhone { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(250)]
        public string Address { get; set; } = string.Empty; 


        
        public ICollection<PharmacyRequestItem> Items { get; set; } = new List<PharmacyRequestItem>();

        [MaxLength(255)]
        public string? PrescriptionImagePath { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public PharmacyRequestStatus Status { get; set; } = PharmacyRequestStatus.New;

        [MaxLength(64)]
        public string? SubmissionToken { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];
    }
}
