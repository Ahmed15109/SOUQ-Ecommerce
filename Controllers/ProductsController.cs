
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

        [HttpGet]
        public async Task<IActionResult> ConfigureWeight(int id)
        {
            if (!User.Identity.IsAuthenticated)
            {
                string returnUrl = Url.Action("ConfigureWeight", "Products", new { id = id });
                return Redirect("/Account/Login?returnUrl=" + System.Net.WebUtility.UrlEncode(returnUrl));
            }

            var product = await _context.Products
                .Include(p => p.WeightTiers)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            if (product.SellingMode != SellingMode.ByWeight)
            {
                return RedirectToAction("Index");
            }

            var viewModel = new EcommerceApp.ViewModels.ProductWeightConfigViewModel
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ProductImageUrl = product.ImageUrl,
                SellingMode = product.SellingMode,
                MinKg = product.MinKg ?? 1,
                MaxKg = product.MaxKg ?? 10,
                StepKg = product.StepKg ?? 0.1m,
                AllowCutting = product.AllowCutting,
                CuttingFee = product.CuttingFee,
                PricePerKg = product.PricePerKg > 0 ? product.PricePerKg : product.Price,
                SelectedWeight = product.MinKg ?? 1
            };

            return View(viewModel);
        }
    }
}
