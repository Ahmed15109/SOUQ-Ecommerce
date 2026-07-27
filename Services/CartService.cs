using System.Security.Claims;
using EcommerceApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Services
{
    public interface ICartService
    {
        Task<int> GetCartCountAsync(ClaimsPrincipal user);
    }

    public class CartService : ICartService
    {
        private readonly AppDbContext _context;

        public CartService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<int> GetCartCountAsync(ClaimsPrincipal user)
        {
            if (user.Identity?.IsAuthenticated != true)
            {
                return 0;
            }

            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return 0;
            }

            return await _context.DbCartItems
                .Where(item => item.Cart.UserId == userId)
                .SumAsync(item => (int?)item.Quantity) ?? 0;
        }
    }
}
