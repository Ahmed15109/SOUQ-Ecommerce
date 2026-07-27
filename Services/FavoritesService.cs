using System.Security.Claims;
using System.Text.Json;
using EcommerceApp.Constants;
using EcommerceApp.Data;
using EcommerceApp.Models;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Services
{
    public class FavoritesService : IFavoritesService
    {
        private const string SessionKey = "Favorites";
        private readonly AppDbContext _context;

        public FavoritesService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<int>> GetFavoriteProductIdsAsync(ClaimsPrincipal? user, ISession session)
        {
            var userId = GetAuthenticatedUserId(user);
            if (userId != null)
            {
                return await _context.UserFavorites
                    .AsNoTracking()
                    .Where(favorite => favorite.UserId == userId)
                    .Select(favorite => favorite.ProductId)
                    .ToListAsync();
            }

            return GetSessionFavorites(session);
        }

        public async Task<int> GetFavoriteCountAsync(ClaimsPrincipal? user, ISession session)
        {
            var userId = GetAuthenticatedUserId(user);
            return userId != null
                ? await _context.UserFavorites.CountAsync(favorite => favorite.UserId == userId)
                : GetSessionFavorites(session).Count;
        }

        public async Task<List<Product>> GetFavoriteProductsAsync(ClaimsPrincipal? user, ISession session)
        {
            var favoriteIds = await GetFavoriteProductIdsAsync(user, session);
            if (favoriteIds.Count == 0)
            {
                return [];
            }

            var favoriteProducts = await _context.Products
                .AsNoTracking()
                .Where(product => favoriteIds.Contains(product.Id))
                .OrderBy(product => product.Name)
                .ToListAsync();

            favoriteProducts.ForEach(product => product.IsFavorite = true);
            return favoriteProducts;
        }

        public async Task<(bool Success, string? Message, bool IsFavorite, int Count)> ToggleFavoriteAsync(
            int productId,
            ClaimsPrincipal? user,
            ISession session)
        {
            if (!await _context.Products.AnyAsync(product => product.Id == productId))
            {
                return (false, "المنتج غير موجود.", false, await GetFavoriteCountAsync(user, session));
            }

            var userId = GetAuthenticatedUserId(user);
            if (userId == null)
            {
                return ToggleSessionFavorite(productId, session);
            }

            var existing = await _context.UserFavorites
                .SingleOrDefaultAsync(favorite => favorite.UserId == userId && favorite.ProductId == productId);

            var intendedState = existing == null;
            if (existing != null)
            {
                _context.UserFavorites.Remove(existing);
            }
            else
            {
                var count = await _context.UserFavorites.CountAsync(favorite => favorite.UserId == userId);
                if (count >= CommerceLimits.MaxAuthenticatedFavorites)
                {
                    return (false, $"الحد الأقصى للمفضلة هو {CommerceLimits.MaxAuthenticatedFavorites}.", false, count);
                }

                _context.UserFavorites.Add(new UserFavorite
                {
                    UserId = userId,
                    ProductId = productId,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                foreach (var entry in _context.ChangeTracker.Entries<UserFavorite>()
                             .Where(entry => entry.State is EntityState.Added or EntityState.Deleted))
                {
                    entry.State = EntityState.Detached;
                }

                var persistedState = await _context.UserFavorites
                    .AsNoTracking()
                    .AnyAsync(favorite => favorite.UserId == userId && favorite.ProductId == productId);

                var persistedCount = await _context.UserFavorites.CountAsync(favorite => favorite.UserId == userId);
                if (persistedState != intendedState)
                {
                    return (false, "تعذر تحديث المفضلة. يرجى المحاولة مرة أخرى.", persistedState, persistedCount);
                }
            }

            return (
                true,
                null,
                intendedState,
                await _context.UserFavorites.CountAsync(favorite => favorite.UserId == userId));
        }

        public async Task MergeSessionFavoritesAsync(string userId, ISession session)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            var sessionFavorites = GetSessionFavorites(session);
            if (sessionFavorites.Count == 0)
            {
                session.Remove(SessionKey);
                return;
            }

            var existingIds = await _context.UserFavorites
                .Where(favorite => favorite.UserId == userId)
                .Select(favorite => favorite.ProductId)
                .ToListAsync();

            var remainingCapacity = Math.Max(0, CommerceLimits.MaxAuthenticatedFavorites - existingIds.Count);
            var candidateIds = sessionFavorites
                .Except(existingIds)
                .Take(remainingCapacity)
                .ToList();

            var validIds = await _context.Products
                .Where(product => candidateIds.Contains(product.Id))
                .Select(product => product.Id)
                .ToListAsync();

            foreach (var productId in validIds)
            {
                _context.UserFavorites.Add(new UserFavorite
                {
                    UserId = userId,
                    ProductId = productId,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                foreach (var entry in _context.ChangeTracker.Entries<UserFavorite>()
                             .Where(entry => entry.State == EntityState.Added))
                {
                    entry.State = EntityState.Detached;
                }

                var persistedIds = await _context.UserFavorites
                    .AsNoTracking()
                    .Where(favorite => favorite.UserId == userId && validIds.Contains(favorite.ProductId))
                    .Select(favorite => favorite.ProductId)
                    .ToListAsync();

                if (validIds.Except(persistedIds).Any())
                {
                    return;
                }
            }

            session.Remove(SessionKey);
        }

        private static string? GetAuthenticatedUserId(ClaimsPrincipal? user) =>
            user?.Identity?.IsAuthenticated == true
                ? user.FindFirstValue(ClaimTypes.NameIdentifier)
                : null;

        private static List<int> GetSessionFavorites(ISession session)
        {
            var sessionData = session.GetString(SessionKey);
            if (string.IsNullOrWhiteSpace(sessionData))
            {
                return [];
            }

            try
            {
                return (JsonSerializer.Deserialize<List<int>>(sessionData) ?? [])
                    .Distinct()
                    .Take(CommerceLimits.MaxAnonymousFavorites)
                    .ToList();
            }
            catch (JsonException)
            {
                session.Remove(SessionKey);
                return [];
            }
        }

        private static void SaveSessionFavorites(ISession session, List<int> favorites)
        {
            session.SetString(SessionKey, JsonSerializer.Serialize(favorites));
        }

        private static (bool Success, string? Message, bool IsFavorite, int Count) ToggleSessionFavorite(
            int productId,
            ISession session)
        {
            var favorites = GetSessionFavorites(session);
            if (favorites.Remove(productId))
            {
                SaveSessionFavorites(session, favorites);
                return (true, null, false, favorites.Count);
            }

            if (favorites.Count >= CommerceLimits.MaxAnonymousFavorites)
            {
                return (
                    false,
                    $"الحد الأقصى للمفضلة هو {CommerceLimits.MaxAnonymousFavorites}.",
                    false,
                    favorites.Count);
            }

            favorites.Add(productId);
            SaveSessionFavorites(session, favorites);
            return (true, null, true, favorites.Count);
        }
    }
}
