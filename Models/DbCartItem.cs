using System.ComponentModel.DataAnnotations.Schema;

namespace EcommerceApp.Models
{
    public class DbCartItem
    {
        public int Id { get; set; }

        public int CartId { get; set; }
        public Cart Cart { get; set; } = null!;

        public int ProductId { get; set; }
        public int Quantity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPriceSnapshot { get; set; }
        
        public string ProductNameSnapshot { get; set; } = string.Empty;
        public string ImageUrlSnapshot { get; set; } = string.Empty;
    }
}
