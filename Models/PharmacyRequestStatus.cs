using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.Models
{
    public enum PharmacyRequestStatus
    {
        [Display(Name = "جديد")]
        New,
        
        [Display(Name = "قيد المعالجة")]
        Processing,
        
        [Display(Name = "تم الشحن")]
        Shipped,
        
        [Display(Name = "تم التسليم")]
        Delivered,
        
        [Display(Name = "ملغي")]
        Cancelled
    }
}
