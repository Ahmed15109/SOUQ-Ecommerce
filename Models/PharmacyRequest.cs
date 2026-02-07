using System;
using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models
{
    public class PharmacyRequest
    {
        public int Id { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string? UserId { get; set; } // Nullable for guests if we allow guest checkout, but usually we prefer logged in. 
                                            // Requirements said "string? if user logged in, else null"

        public string? UserPhone { get; set; }
        
        [Required]
        public string FullName { get; set; } // For guest or overriding user name

        [Required]
        public string Address { get; set; } = string.Empty; // Full address string
        


        // Replaced Json with Relational Table
        // public string MedicinesJson { get; set; } = "[]"; 
        public ICollection<PharmacyRequestItem> Items { get; set; } = new List<PharmacyRequestItem>();

        public string? PrescriptionImagePath { get; set; }

        public string? Notes { get; set; }

        public PharmacyRequestStatus Status { get; set; } = PharmacyRequestStatus.New;
    }
}
