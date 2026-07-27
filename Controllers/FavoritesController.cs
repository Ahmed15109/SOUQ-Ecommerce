using Microsoft.AspNetCore.Mvc;
using EcommerceApp.Services;

namespace EcommerceApp.Controllers
{
    public class FavoritesController : Controller
    {
        private readonly IFavoritesService _favoritesService;

        public FavoritesController(IFavoritesService favoritesService)
        {
            _favoritesService = favoritesService;
        }

        public async Task<IActionResult> Index()
        {
            var favoriteProducts = await _favoritesService.GetFavoriteProductsAsync(User, HttpContext.Session);
            return View(favoriteProducts);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int id)
        {
            var (success, message, isFavorite, count) = await _favoritesService.ToggleFavoriteAsync(id, User, HttpContext.Session);
            return Json(new { success = success, message = message, count = count, isFavorite = isFavorite });
        }
    }
}
