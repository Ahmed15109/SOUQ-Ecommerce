using System.ComponentModel.DataAnnotations;

namespace EcommerceApp.ViewModels
{
    public class MedicineRowVM
    {
        [MaxLength(150)]
        public string? Name { get; set; }

        [Range(1, 100, ErrorMessage = "الكمية يجب أن تكون بين 1 و100.")]
        public int? Quantity { get; set; }
    }

    public class PharmacyRequestVM
    {
        public List<MedicineRowVM> Medicines { get; set; } = [];

        public IFormFile? PrescriptionImage { get; set; }

        [MaxLength(1000)]
        public string? Notes { get; set; }

        [Required(ErrorMessage = "الاسم الكامل مطلوب.")]
        [Display(Name = "الاسم الكامل")]
        [MaxLength(100)]
        public string? FullName { get; set; }

        [Required(ErrorMessage = "رقم الهاتف مطلوب.")]
        [RegularExpression(@"^01[0125]\d{8}$", ErrorMessage = "رقم الهاتف المصري غير صحيح.")]
        [Display(Name = "رقم الهاتف")]
        public string UserPhone { get; set; } = string.Empty;

        [Required(ErrorMessage = "العنوان مطلوب.")]
        [Display(Name = "العنوان بالتفصيل")]
        [MaxLength(250)]
        public string Address { get; set; } = string.Empty;

        [Required]
        [StringLength(64, MinimumLength = 32)]
        public string SubmissionToken { get; set; } = string.Empty;
    }
}
