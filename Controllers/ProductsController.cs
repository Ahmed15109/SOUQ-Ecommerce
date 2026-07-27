
using Microsoft.AspNetCore.Mvc;
using EcommerceApp.Models;
using EcommerceApp.Data;
using EcommerceApp.Extensions;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Controllers
{
    public class ProductsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IFavoritesService _favoritesService;

        public ProductsController(AppDbContext context, IFavoritesService favoritesService)
        {
            _context = context;
            _favoritesService = favoritesService;
        }

        public async Task<IActionResult> Index(int? categoryId, string? search, int page = 1, int pageSize = 12)
        {
            var categoryName = "كل المنتجات";
            var productsQuery = _context.Products.AsNoTracking().AsQueryable();

            if (categoryId.HasValue)
            {
                var category = await _context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == categoryId.Value);
                if (category == null)
                {
                    return NotFound();
                }

                categoryName = category.Name;
                productsQuery = productsQuery.Where(p => p.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                if (search.Length > 100)
                {
                    search = search[..100];
                }

                productsQuery = productsQuery.Where(product =>
                    product.Name.Contains(search) || product.Description.Contains(search));
            }

            ViewData["CategoryName"] = categoryName;
            ViewData["CategoryId"] = categoryId;
            ViewData["Search"] = search;

            var pagedProducts = await productsQuery
                .OrderBy(p => p.Id)
                .ToPagedListAsync(page, pageSize, defaultPageSize: 12, maxPageSize: 60);

            var favoriteIds = (await _favoritesService.GetFavoriteProductIdsAsync(User, HttpContext.Session)).ToHashSet();
            foreach (var product in pagedProducts.Items)
            {
                product.IsFavorite = favoriteIds.Contains(product.Id);
            }

            return View(pagedProducts);
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ConfigureWeight(int id)
        {
            var product = await _context.Products
                .AsNoTracking()
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
                MinKg = product.MinKg ?? 1,
                MaxKg = product.MaxKg ?? 10,
                StepKg = product.StepKg ?? 0.1m,
                AllowCutting = product.AllowCutting,
                CuttingFee = product.CuttingFee,
                PricePerKg = product.PricePerKg,
                SelectedWeight = product.MinKg ?? 1,
                WeightTiers = product.WeightTiers
                    .OrderBy(tier => tier.FromKg)
                    .Select(tier => new EcommerceApp.ViewModels.WeightTierPriceViewModel
                    {
                        FromKg = tier.FromKg,
                        ToKg = tier.ToKg,
                        PricePerKg = tier.PricePerKg
                    })
                    .ToList()
            };

            return View(viewModel);
        }
    }
}
