
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceApp.Models
{
    public enum OrderStatus
    {
        [Display(Name = "قيد الانتظار")]
        Pending,
        
        [Display(Name = "قيد المعالجة")]
        Processing,
        
        [Display(Name = "تم الشحن")]
        Shipped,
        
        [Display(Name = "تم التسليم")]
        Delivered,
        
        [Display(Name = "ملغي")]
        Canceled
    }

    public class Order
    {
        public int Id { get; set; }

        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }

        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string City { get; set; } = string.Empty;
        [Required]
        [MaxLength(100)]
        public string Area { get; set; } = string.Empty;
        [Required]
        [MaxLength(200)]
        public string Street { get; set; } = string.Empty;
        [Required]
        [MaxLength(50)]
        public string Building { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Notes { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DeliveryFee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<OrderItem> OrderItems { get; set; } = new();

        [MaxLength(64)]
        public string? IdempotencyKey { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];

    }
}
