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

        private async Task<Cart?> GetOrCreateUserCartAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return null;
            }

            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
            {
                return null;
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
            if (cart == null)
            {
                return Json(new { success = false, message = "يرجى تسجيل الدخول أولاً", redirect = "/Account/Login" });
            }
            
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return Json(new { success = false, message = "المنتج غير موجود" });
            }
            
            if (product.SellingMode == SellingMode.ByWeight)
            {
                return Json(new { success = false, message = "هذا المنتج يُباع بالوزن، يرجى اختيار الوزن أولاً", redirect = $"/Products/ConfigureWeight/{id}" });
            }
            
            var existingItem = await _context.DbCartItems
                .FirstOrDefaultAsync(ci => ci.CartId == cart.Id && ci.ProductId == id);

            if (existingItem != null)
            {
                existingItem.Quantity++;
                _context.DbCartItems.Update(existingItem);
            }
            else
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
                .FirstOrDefaultAsync(ci => ci.Id == id && ci.CartId == cart.Id);

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
            decimal deliveryFee = _configuration.GetValue<decimal>("ShopSettings:DeliveryFee", 15);
            decimal finalTotal = cartTotal + deliveryFee;

            return Json(new { success = true, count = totalCount, cartTotal, deliveryFee, finalTotal });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateQuantity(int id, int qty)
        {
            var cart = await GetOrCreateUserCartAsync();
            
            var item = await _context.DbCartItems
                .FirstOrDefaultAsync(ci => ci.Id == id && ci.CartId == cart.Id);

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
                decimal deliveryFee = _configuration.GetValue<decimal>("ShopSettings:DeliveryFee", 15);
                decimal finalTotal = cartTotal + deliveryFee;

                return Json(new { success = true, itemTotal, cartTotal, count = totalCount, deliveryFee, finalTotal });
            }

            return Json(new { success = false });
        }
        [HttpPost]
        public async Task<IActionResult> AddWeightItem(int productId, decimal weight, bool isCutting)
        {
            if (!User.Identity.IsAuthenticated)
            {
                string returnUrl = Url.Action("ConfigureWeight", "Products", new { id = productId });
                return Json(new { success = false, redirect = "/Account/Login?returnUrl=" + System.Net.WebUtility.UrlEncode(returnUrl) });
            }

            var cart = await GetOrCreateUserCartAsync();
            if (cart == null)
            {
                // This fallback should rarely replace the explicit check above, 
                // but keeps existing logic safe.
                return Json(new { success = false, message = "يرجى تسجيل الدخول أولاً", redirect = "/Account/Login" });
            }

            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);
            if (product == null || product.SellingMode != SellingMode.ByWeight)
            {
                return Json(new { success = false, message = "المنتج غير موجود أو لا يباع بالوزن" });
            }

            if (weight < (product.MinKg ?? 0) || weight > (product.MaxKg ?? 10000))
            {
                 return Json(new { success = false, message = "الوزن غير صحيح" });
            }

            decimal pricePerKg = product.PricePerKg;
            
            decimal cuttingFee = 0;
            if (isCutting && product.AllowCutting)
            {
                cuttingFee = product.CuttingFee;
            }

            decimal finalUnitPrice = (weight * pricePerKg) + cuttingFee;

            var existingItem = await _context.DbCartItems
                .FirstOrDefaultAsync(ci => ci.CartId == cart.Id 
                                        && ci.ProductId == productId
                                        && ci.SelectedWeightKg == weight
                                        && ci.CuttingSelected == isCutting);

            if (existingItem != null)
            {
                existingItem.Quantity++;
                _context.DbCartItems.Update(existingItem);
            }
            else
            {
                var cartItem = new DbCartItem
                {
                    CartId = cart.Id,
                    ProductId = product.Id,
                    Quantity = 1,
                    UnitPriceSnapshot = finalUnitPrice,
                    ProductNameSnapshot = product.Name + $" ({weight} كجم)",
                    ImageUrlSnapshot = product.ImageUrl,
                    
                    SelectedWeightKg = weight,
                    SelectedPricePerKg = pricePerKg,
                    CuttingSelected = isCutting,
                    CuttingFeeApplied = cuttingFee
                };
                _context.DbCartItems.Add(cartItem);
            }

            await _context.SaveChangesAsync();

            var totalCount = await _context.DbCartItems
                .Where(ci => ci.CartId == cart.Id)
                .SumAsync(ci => ci.Quantity);

            return Json(new { success = true, count = totalCount });
        }
    }
}
