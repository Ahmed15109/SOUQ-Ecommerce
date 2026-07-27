using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Options
{
    public class ShopSettings
    {
        public const string SectionName = "ShopSettings";

        [Required]
        [MaxLength(100)]
        public string StoreName { get; set; } = "SOUQ";

        [Range(0, 10000)]
        public decimal DeliveryFee { get; set; } = 15m;

        [MaxLength(20)]
        [RegularExpression(@"^\+?[0-9]{8,15}$")]
        public string ContactPhone { get; set; } = string.Empty;

        [EmailAddress]
        public string ContactEmail { get; set; } = string.Empty;

        [Url]
        public string WhatsAppChannelUrl { get; set; } = string.Empty;
    }
}
