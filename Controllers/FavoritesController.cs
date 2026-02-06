
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using EcommerceApp.Models;
using EcommerceApp.Data;

namespace EcommerceApp.Controllers
{
    public class FavoritesController : Controller
    {
        private readonly AppDbContext _context;

        public FavoritesController(AppDbContext context)
        {
            _context = context;
        }

        private List<int> GetFavorites()
        {
            var sessionData = HttpContext.Session.GetString("Favorites");
            return string.IsNullOrEmpty(sessionData) ? new List<int>() : JsonSerializer.Deserialize<List<int>>(sessionData) ?? new List<int>();
        }

        private void SaveFavorites(List<int> favorites)
        {
            HttpContext.Session.SetString("Favorites", JsonSerializer.Serialize(favorites));
        }

        public IActionResult Index()
        {
            var favoriteIds = GetFavorites();
            var favoriteProducts = _context.Products.Where(p => favoriteIds.Contains(p.Id)).ToList();
            
            foreach(var p in favoriteProducts) p.IsFavorite = true;

            return View(favoriteProducts);
        }

        [HttpPost]
        public IActionResult Toggle(int id)
        {
            var favorites = GetFavorites();
            bool isFavorite;

            if (favorites.Contains(id))
            {
                favorites.Remove(id);
                isFavorite = false;
            }
            else
            {
                favorites.Add(id);
                isFavorite = true;
            }

            SaveFavorites(favorites);

            return Json(new { success = true, count = favorites.Count, isFavorite = isFavorite });
        }
    }
}
