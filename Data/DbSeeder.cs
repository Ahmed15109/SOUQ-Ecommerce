
using EcommerceApp.Models;

namespace EcommerceApp.Data
{
    public class DbSeeder
    {
        public static void Seed(AppDbContext context)
        {
            if (!context.Categories.Any())
            {
                var categories = new List<Category>
                {
                    new Category { Name = "Vegetables" },
                    new Category { Name = "Supermarket" },
                    new Category { Name = "Pharmacy" },
                    new Category { Name = "Restaurants" }
                };
                context.Categories.AddRange(categories);
                context.SaveChanges();
            }

            if (!context.Products.Any())
            {
                var veg = context.Categories.FirstOrDefault(c => c.Name == "Vegetables");
                var supermarket = context.Categories.FirstOrDefault(c => c.Name == "Supermarket");
                var pharmacy = context.Categories.FirstOrDefault(c => c.Name == "Pharmacy");
                var restaurants = context.Categories.FirstOrDefault(c => c.Name == "Restaurants");

                if (veg != null && supermarket != null && pharmacy != null && restaurants != null)
                {
                     var products = new List<Product>
                    {
                        new Product { Name = "Fresh Tomato", Price = 2.50m, CategoryId = veg.Id, ImageUrl = "https://via.placeholder.com/300x200?text=Tomato", IsFavorite = false },
                        new Product { Name = "Cucumber", Price = 1.20m, CategoryId = veg.Id, ImageUrl = "https://via.placeholder.com/300x200?text=Cucumber", IsFavorite = true },
                        new Product { Name = "Milk 1L", Price = 1.50m, CategoryId = supermarket.Id, ImageUrl = "https://via.placeholder.com/300x200?text=Milk", IsFavorite = false },
                        new Product { Name = "Cheese Block", Price = 5.00m, CategoryId = supermarket.Id, ImageUrl = "https://via.placeholder.com/300x200?text=Cheese", IsFavorite = false },
                        new Product { Name = "Painkiller", Price = 10.00m, CategoryId = pharmacy.Id, ImageUrl = "https://via.placeholder.com/300x200?text=Painkiller", IsFavorite = false },
                        new Product { Name = "Vitamins", Price = 25.00m, CategoryId = pharmacy.Id, ImageUrl = "https://via.placeholder.com/300x200?text=Vitamins", IsFavorite = true },
                        new Product { Name = "Burger Meal", Price = 12.00m, CategoryId = restaurants.Id, ImageUrl = "https://via.placeholder.com/300x200?text=Burger", IsFavorite = false },
                        new Product { Name = "Pizza", Price = 15.00m, CategoryId = restaurants.Id, ImageUrl = "https://via.placeholder.com/300x200?text=Pizza", IsFavorite = false },
                    };
                    context.Products.AddRange(products);
                    context.SaveChanges();
                }
            }
        }
    }
}
