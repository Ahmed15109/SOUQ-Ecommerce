
using EcommerceApp.Models;

namespace EcommerceApp.ViewModels
{
    public class HomeViewModel
    {
        public List<Product> FeaturedProducts { get; set; } = new();
        public List<CategoryCardViewModel> Categories { get; set; } = new();
        public Dictionary<int, string> CategoryNames { get; set; } = new();
    }
}
