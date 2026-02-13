using System;
using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models
{
    public class PharmacyRequest
    {
        public int Id { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string? UserId { get; set; }

        public string? UserPhone { get; set; }
        
        [Required]
        public string FullName { get; set; } 

        [Required]
        public string Address { get; set; } = string.Empty; 


        
        public ICollection<PharmacyRequestItem> Items { get; set; } = new List<PharmacyRequestItem>();

        public string? PrescriptionImagePath { get; set; }

        public string? Notes { get; set; }

        public PharmacyRequestStatus Status { get; set; } = PharmacyRequestStatus.New;
    }
}
