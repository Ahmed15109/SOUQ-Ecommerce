using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using EcommerceApp.Models;
using EcommerceApp.Data;

namespace EcommerceApp.Controllers
{
    [Authorize] 
    public class CartController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public CartController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        private async Task<Cart> GetOrCreateUserCartAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("User not logged in");
            }

            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                cart = new Cart { UserId = userId };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            return cart;
        }

        public async Task<IActionResult> Index()
        {
            var cart = await GetOrCreateUserCartAsync();
            
            var cartItems = await _context.DbCartItems
                .Where(ci => ci.CartId == cart.Id)
                .ToListAsync();

            decimal subtotal = cartItems.Sum(item => item.UnitPriceSnapshot * item.Quantity);
            decimal deliveryFee = _configuration.GetValue<decimal>("ShopSettings:DeliveryFee", 15);
            decimal total = subtotal + deliveryFee;

            ViewBag.Subtotal = subtotal;
            ViewBag.DeliveryFee = deliveryFee;
            ViewBag.Total = total;

            return View(cartItems);
        }

        [HttpPost]
        public async Task<IActionResult> AddToCart(int id)
        {
            var cart = await GetOrCreateUserCartAsync();
            
            var existingItem = await _context.DbCartItems
                .FirstOrDefaultAsync(ci => ci.CartId == cart.Id && ci.ProductId == id);

            if (existingItem != null)
            {
                existingItem.Quantity++;
                _context.DbCartItems.Update(existingItem);
            }
            else
            {
                var product = await _context.Products.FindAsync(id);
                if (product != null)
                {
                    var cartItem = new DbCartItem
                    {
                        CartId = cart.Id,
                        ProductId = product.Id,
                        Quantity = 1,
                        UnitPriceSnapshot = product.Price,
                        ProductNameSnapshot = product.Name,
                        ImageUrlSnapshot = product.ImageUrl
                    };
                    _context.DbCartItems.Add(cartItem);
                }
            }

            await _context.SaveChangesAsync();
            
            var totalCount = await _context.DbCartItems
                .Where(ci => ci.CartId == cart.Id)
                .SumAsync(ci => ci.Quantity);

            return Json(new { success = true, count = totalCount });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveFromCart(int id)
        {
            var cart = await GetOrCreateUserCartAsync();
            
            var item = await _context.DbCartItems
                .FirstOrDefaultAsync(ci => ci.CartId == cart.Id && ci.ProductId == id);

            if (item != null)
            {
                _context.DbCartItems.Remove(item);
                await _context.SaveChangesAsync();
            }

            var cartItems = await _context.DbCartItems
                .Where(ci => ci.CartId == cart.Id)
                .ToListAsync();

            var totalCount = cartItems.Sum(ci => ci.Quantity);
            var cartTotal = cartItems.Sum(ci => ci.UnitPriceSnapshot * ci.Quantity);

            return Json(new { success = true, count = totalCount, cartTotal });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int id, int qty)
        {
            var cart = await GetOrCreateUserCartAsync();
            
            var item = await _context.DbCartItems
                .FirstOrDefaultAsync(ci => ci.CartId == cart.Id && ci.ProductId == id);

            if (item != null)
            {
                if (qty <= 0)
                {
                    _context.DbCartItems.Remove(item);
                }
                else
                {
                    item.Quantity = qty;
                    _context.DbCartItems.Update(item);
                }
                
                await _context.SaveChangesAsync();

                var cartItems = await _context.DbCartItems
                    .Where(ci => ci.CartId == cart.Id)
                    .ToListAsync();

                var itemTotal = item.UnitPriceSnapshot * item.Quantity;
                var cartTotal = cartItems.Sum(ci => ci.UnitPriceSnapshot * ci.Quantity);
                var totalCount = cartItems.Sum(ci => ci.Quantity);

                return Json(new { success = true, itemTotal, cartTotal, count = totalCount });
            }

            return Json(new { success = false });
        }
    }
}
