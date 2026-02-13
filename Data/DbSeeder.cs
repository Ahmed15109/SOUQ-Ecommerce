using EcommerceApp.Models;

namespace EcommerceApp.Data
{
    public class DbSeeder
    {
        public static void Seed(AppDbContext context)
        {
            var categoryNames = new[]
            {
                "الخضار",
                "السوبر ماركت",
                "الصيدلية",
                "المطاعم",
                "الدواجن"
            };

            foreach (var name in categoryNames)
            {
                if (!context.Categories.Any(c => c.Name == name))
                {
                    context.Categories.Add(new Category { Name = name });
                }
            }
            context.SaveChanges();

            if (!context.Products.Any())
            {
                var veg = context.Categories.First(c => c.Name == "الخضار");
                var supermarket = context.Categories.First(c => c.Name == "السوبر ماركت");
                var pharmacy = context.Categories.First(c => c.Name == "الصيدلية");
                var restaurants = context.Categories.First(c => c.Name == "المطاعم");

                var products = new List<Product>
                {
                    new Product { Name = "طماطم طازجة", Price = 2.50m, CategoryId = veg.Id, ImageUrl = "https://via.placeholder.com/300x200?text=Tomato", IsFavorite = false, Description = "طماطم طازجة يوميًا" },
                    new Product { Name = "خيار", Price = 1.20m, CategoryId = veg.Id, ImageUrl = "https://via.placeholder.com/300x200?text=Cucumber", IsFavorite = true, Description = "خيار طازج" },

                };

                context.Products.AddRange(products);
                context.SaveChanges();
            }
        }
    }
}

