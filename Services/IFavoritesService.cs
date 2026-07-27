using System.Security.Claims;
using EcommerceApp.Models;
using Microsoft.AspNetCore.Http;

namespace EcommerceApp.Services
{
    public interface IFavoritesService
    {
        Task<List<int>> GetFavoriteProductIdsAsync(ClaimsPrincipal? user, ISession session);
        Task<int> GetFavoriteCountAsync(ClaimsPrincipal? user, ISession session);
        Task<List<Product>> GetFavoriteProductsAsync(ClaimsPrincipal? user, ISession session);
        Task<(bool Success, string? Message, bool IsFavorite, int Count)> ToggleFavoriteAsync(int productId, ClaimsPrincipal? user, ISession session);
        Task MergeSessionFavoritesAsync(string userId, ISession session);
    }
}
