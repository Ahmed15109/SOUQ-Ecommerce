using System.Data;
using System.Security.Claims;
using EcommerceApp.Constants;
using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Options;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EcommerceApp.Controllers
{
    [Authorize]
    public class CartController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IProductPricingService _pricingService;
        private readonly ShopSettings _shopSettings;

        public CartController(
            AppDbContext context,
            IProductPricingService pricingService,
            IOptions<ShopSettings> shopSettings)
        {
            _context = context;
            _pricingService = pricingService;
            _shopSettings = shopSettings.Value;
        }

        public async Task<IActionResult> Index()
        {
            var cart = await GetOrCreateUserCartAsync();
            if (cart == null)
            {
                return Challenge();
            }

            var items = await _context.DbCartItems
                .AsNoTracking()
                .Where(item => item.CartId == cart.Id)
                .OrderBy(item => item.Id)
                .ToListAsync();

            var subtotal = items.Sum(item => item.UnitPriceSnapshot * item.Quantity);
            ViewBag.Subtotal = subtotal;
            ViewBag.DeliveryFee = _shopSettings.DeliveryFee;
            ViewBag.Total = subtotal + _shopSettings.DeliveryFee;
            return View(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(int id)
        {
            var cart = await GetOrCreateUserCartAsync();
            if (cart == null)
            {
                return Json(new { success = false, message = "يرجى تسجيل الدخول أولاً.", redirect = "/Account/Login" });
            }

            var product = await _context.Products.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id);
            if (product == null)
            {
                return Json(new { success = false, message = "المنتج غير موجود." });
            }

            if (product.SellingMode == SellingMode.ByWeight)
            {
                return Json(new
                {
                    success = false,
                    message = "هذا المنتج يباع بالوزن. يرجى اختيار الوزن أولاً.",
                    redirect = Url.Action("ConfigureWeight", "Products", new { id })
                });
            }

            return await AddOrIncrementAsync(cart, product, null, false);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddWeightItem(int productId, decimal weight, bool isCutting)
        {
            var cart = await GetOrCreateUserCartAsync();
            if (cart == null)
            {
                var returnUrl = Url.Action("ConfigureWeight", "Products", new { id = productId }) ?? "/";
                return Json(new
                {
                    success = false,
                    redirect = "/Account/Login?returnUrl=" + System.Net.WebUtility.UrlEncode(returnUrl)
                });
            }

            var product = await _context.Products
                .AsNoTracking()
                .Include(item => item.WeightTiers)
                .SingleOrDefaultAsync(item => item.Id == productId);

            if (product == null || product.SellingMode != SellingMode.ByWeight)
            {
                return Json(new { success = false, message = "المنتج غير موجود أو لا يباع بالوزن." });
            }

            return await AddOrIncrementAsync(cart, product, weight, isCutting);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFromCart(int id)
        {
            var cart = await GetOrCreateUserCartAsync();
            if (cart == null)
            {
                return Json(new { success = false, message = "غير مصرح." });
            }

            var item = await _context.DbCartItems
                .SingleOrDefaultAsync(cartItem => cartItem.Id == id && cartItem.CartId == cart.Id);

            if (item != null)
            {
                _context.DbCartItems.Remove(item);
                await _context.SaveChangesAsync();
            }

            return Json(await GetTotalsAsync(cart.Id));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateQuantity(int id, int qty)
        {
            var cart = await GetOrCreateUserCartAsync();
            if (cart == null)
            {
                return Json(new { success = false, message = "غير مصرح." });
            }

            if (qty > CommerceLimits.MaxQuantityPerLine)
            {
                return Json(new
                {
                    success = false,
                    message = $"الحد الأقصى للكمية هو {CommerceLimits.MaxQuantityPerLine}."
                });
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                var item = await _context.DbCartItems
                    .SingleOrDefaultAsync(cartItem => cartItem.Id == id && cartItem.CartId == cart.Id);

                if (item == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "عنصر السلة غير موجود."
                    });
                }

                if (qty <= 0)
                {
                    _context.DbCartItems.Remove(item);
                }
                else
                {
                    var otherQuantity = await _context.DbCartItems
                        .Where(cartItem => cartItem.CartId == cart.Id && cartItem.Id != id)
                        .SumAsync(cartItem => (int?)cartItem.Quantity) ?? 0;

                    if (otherQuantity + qty > CommerceLimits.MaxTotalCartQuantity)
                    {
                        return Json(new
                        {
                            success = false,
                            message = $"الحد الأقصى لإجمالي عناصر السلة هو {CommerceLimits.MaxTotalCartQuantity}."
                        });
                    }

                    item.Quantity = qty;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var totals = await GetTotalsAsync(cart.Id);
                return Json(new
                {
                    success = totals.Success,
                    itemTotal = qty <= 0 ? 0m : item.UnitPriceSnapshot * qty,
                    cartTotal = totals.CartTotal,
                    count = totals.Count,
                    deliveryFee = totals.DeliveryFee,
                    finalTotal = totals.FinalTotal
                });
            });
        }

        private async Task<IActionResult> AddOrIncrementAsync(
            Cart cart,
            Product product,
            decimal? weight,
            bool cuttingSelected)
        {
            var price = _pricingService.Calculate(product, weight, cuttingSelected);
            if (!price.IsValid)
            {
                return Json(new { success = false, message = price.ErrorMessage });
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

                var totalQuantity = await _context.DbCartItems
                    .Where(item => item.CartId == cart.Id)
                    .SumAsync(item => (int?)item.Quantity) ?? 0;

                if (totalQuantity + 1 > CommerceLimits.MaxTotalCartQuantity)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"الحد الأقصى لإجمالي عناصر السلة هو {CommerceLimits.MaxTotalCartQuantity}."
                    });
                }

                var existing = await _context.DbCartItems.SingleOrDefaultAsync(item =>
                    item.CartId == cart.Id &&
                    item.ProductId == product.Id &&
                    item.SelectedWeightKg == price.SelectedWeightKg &&
                    item.CuttingSelected == price.CuttingSelected);

                if (existing != null)
                {
                    if (existing.Quantity >= CommerceLimits.MaxQuantityPerLine)
                    {
                        return Json(new
                        {
                            success = false,
                            message = $"الحد الأقصى للكمية هو {CommerceLimits.MaxQuantityPerLine}."
                        });
                    }

                    existing.Quantity++;
                    existing.UnitPriceSnapshot = price.UnitPrice;
                    existing.ProductNameSnapshot = product.Name;
                    existing.ImageUrlSnapshot = product.ImageUrl;
                    existing.SelectedPricePerKg = price.SelectedPricePerKg;
                    existing.CuttingFeeApplied = price.CuttingFeeApplied;
                }
                else
                {
                    _context.DbCartItems.Add(new DbCartItem
                    {
                        CartId = cart.Id,
                        ProductId = product.Id,
                        Quantity = 1,
                        UnitPriceSnapshot = price.UnitPrice,
                        ProductNameSnapshot = product.Name,
                        ImageUrlSnapshot = product.ImageUrl,
                        SelectedWeightKg = price.SelectedWeightKg,
                        SelectedPricePerKg = price.SelectedPricePerKg,
                        CuttingSelected = price.CuttingSelected,
                        CuttingFeeApplied = price.CuttingFeeApplied
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var count = await _context.DbCartItems
                    .Where(item => item.CartId == cart.Id)
                    .SumAsync(item => (int?)item.Quantity) ?? 0;

                return Json(new { success = true, count });
            });
        }

        private async Task<Cart?> GetOrCreateUserCartAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            var cart = await _context.Carts.SingleOrDefaultAsync(item => item.UserId == userId);
            if (cart != null)
            {
                return cart;
            }

            cart = new Cart { UserId = userId, CreatedAt = DateTime.UtcNow };
            _context.Carts.Add(cart);

            try
            {
                await _context.SaveChangesAsync();
                return cart;
            }
            catch (DbUpdateException)
            {
                _context.Entry(cart).State = EntityState.Detached;
                return await _context.Carts.SingleOrDefaultAsync(item => item.UserId == userId);
            }
        }

        private async Task<CartTotals> GetTotalsAsync(int cartId)
        {
            var items = await _context.DbCartItems
                .AsNoTracking()
                .Where(item => item.CartId == cartId)
                .ToListAsync();

            var cartTotal = items.Sum(item => item.UnitPriceSnapshot * item.Quantity);
            var count = items.Sum(item => item.Quantity);
            return new CartTotals(
                true,
                count,
                cartTotal,
                _shopSettings.DeliveryFee,
                cartTotal + _shopSettings.DeliveryFee);
        }

        private sealed record CartTotals(
            bool Success,
            int Count,
            decimal CartTotal,
            decimal DeliveryFee,
            decimal FinalTotal);
    }
}
