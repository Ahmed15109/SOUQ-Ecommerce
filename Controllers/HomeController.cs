
using Microsoft.AspNetCore.Mvc;
using EcommerceApp.Models;
using EcommerceApp.ViewModels;
using EcommerceApp.Data;
using EcommerceApp.Services;
using EcommerceApp.Constants;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace EcommerceApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IFavoritesService _favoritesService;

        public HomeController(AppDbContext context, IFavoritesService favoritesService)
        {
            _context = context;
            _favoritesService = favoritesService;
        }

        public async Task<IActionResult> Index()
        {
            var categories = await _context.Categories.AsNoTracking().OrderBy(category => category.Id).ToListAsync();
            var products = await _context.Products
                .AsNoTracking()
                .Where(product => product.IsFeatured)
                .OrderByDescending(product => product.IsFeatured)
                .ThenByDescending(product => product.Id)
                .Take(8)
                .ToListAsync();

            var favoriteIds = (await _favoritesService.GetFavoriteProductIdsAsync(User, HttpContext.Session)).ToHashSet();
            products.ForEach(product => product.IsFavorite = favoriteIds.Contains(product.Id));

            var viewModel = new HomeViewModel
            {
                Categories = categories.Select(CreateCategoryCard).ToList(),
                CategoryNames = categories.ToDictionary(category => category.Id, category => category.Name),
                FeaturedProducts = products
            };
            return View(viewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }

        private static CategoryCardViewModel CreateCategoryCard(Category category)
        {
            var (iconClass, themeClass) = category.IsCore
                ? category.Id switch
                {
                    CoreCategoryIds.Produce => ("bi bi-basket3-fill", "category-produce"),
                    CoreCategoryIds.Supermarket => ("bi bi-basket2-fill", "category-supermarket"),
                    CoreCategoryIds.Pharmacy => ("bi bi-capsule", "category-pharmacy"),
                    CoreCategoryIds.Restaurants => ("bi bi-egg-fried", "category-restaurants"),
                    CoreCategoryIds.Poultry => ("bi bi-egg", "category-poultry"),
                    _ => ("bi bi-star-fill", "category-default")
                }
                : ResolveCustomTheme(category);

            return new CategoryCardViewModel
            {
                Id = category.Id,
                Name = category.Name,
                IconClass = iconClass,
                ThemeClass = themeClass,
                IconColor = NormalizeHexColor(category.IconColor),
                IconBackgroundColor = NormalizeHexColor(category.IconBgColor),
                LinksToPharmacyRequests =
                    category.Id == CoreCategoryIds.Pharmacy ||
                    string.Equals(category.IconKey, "pharmacy", StringComparison.OrdinalIgnoreCase)
            };
        }

        private static (string IconClass, string ThemeClass) ResolveCustomTheme(Category category)
        {
            if (!string.IsNullOrWhiteSpace(category.IconClass) &&
                Regex.IsMatch(category.IconClass, @"^[a-zA-Z0-9 _-]+$"))
            {
                return (category.IconClass, "category-default");
            }

            return category.IconKey?.Trim().ToLowerInvariant() switch
            {
                "produce" => ("bi bi-basket3-fill", "category-produce"),
                "supermarket" => ("bi bi-basket2-fill", "category-supermarket"),
                "pharmacy" => ("bi bi-capsule", "category-pharmacy"),
                "restaurant" => ("bi bi-egg-fried", "category-restaurants"),
                _ => ("bi bi-tag", "category-default")
            };
        }

        private static string? NormalizeHexColor(string? value) =>
            !string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, @"^#[0-9a-fA-F]{6}$")
                ? value
                : null;
    }
}
