
using Microsoft.AspNetCore.Mvc;
using EcommerceApp.Models;
using EcommerceApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Controllers
{
    public class ProductsController : Controller
    {
        private readonly AppDbContext _context;

        public ProductsController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? categoryId)
        {
            var categoryName = "All Products";
            var productsQuery = _context.Products.AsQueryable();

            if (categoryId.HasValue)
            {
                var category = await _context.Categories.FindAsync(categoryId.Value);
                if (category != null)
                {
                    categoryName = category.Name;
                    productsQuery = productsQuery.Where(p => p.CategoryId == categoryId.Value);
                }
            }

            ViewData["CategoryName"] = categoryName;
            ViewData["CategoryId"] = categoryId;

            return View(await productsQuery.ToListAsync());
        }
    }
}
