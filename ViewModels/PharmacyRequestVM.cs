using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.ViewModels
{
    public class MedicineRowVM
    {
        [Required(ErrorMessage = "اسم الدواء مطلوب")]
        public string? Name { get; set; }

        [Required(ErrorMessage = "الكمية مطلوبة")]
        [Range(1, int.MaxValue, ErrorMessage = "الكمية يجب أن تكون 1 على الأقل")]
        public int? Quantity { get; set; } 
    }

    public class PharmacyRequestVM
    {
        public List<MedicineRowVM> Medicines { get; set; } = new List<MedicineRowVM>();

        public IFormFile? PrescriptionImage { get; set; }

        public string? Notes { get; set; }

        [Required(ErrorMessage = "الاسم الكامل مطلوب")]
        [Display(Name = "الاسم الكامل")]
        public string? FullName { get; set; }

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [RegularExpression(@"^01[0-2,5]\d{8}$", ErrorMessage = "رقم الهاتف غير صحيح. يجب أن يتكون من 11 رقم ويبدأ بـ 01")]
        [Display(Name = "رقم الهاتف")]
        public string UserPhone { get; set; } = string.Empty;

        [Required(ErrorMessage = "العنوان مطلوب")]
        [Display(Name = "العنوان بالتفصيل")]
        public string Address { get; set; } = string.Empty;

        
        public bool ShowSuccess { get; set; }
    }
}
