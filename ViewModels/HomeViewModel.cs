
using EcommerceApp.Models;

namespace EcommerceApp.ViewModels
{
    public class HomeViewModel
    {
        public List<Product> FeaturedProducts { get; set; } = new();
        public List<Category> Categories { get; set; } = new();
    }
}
