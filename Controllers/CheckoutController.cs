using System.Data;
using System.Security.Claims;
using EcommerceApp.Constants;
using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.Options;
using EcommerceApp.Services;
using EcommerceApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EcommerceApp.Controllers
{
    [Authorize]
    public class CheckoutController : Controller
    {
        private readonly AppDbContext _context;
        private readonly INotificationService _notificationService;
        private readonly PdfInvoiceService _pdfService;
        private readonly IProductPricingService _pricingService;
        private readonly ShopSettings _shopSettings;

        public CheckoutController(
            AppDbContext context,
            PdfInvoiceService pdfService,
            INotificationService notificationService,
            IProductPricingService pricingService,
            IOptions<ShopSettings> shopSettings)
        {
            _context = context;
            _pdfService = pdfService;
            _notificationService = notificationService;
            _pricingService = pricingService;
            _shopSettings = shopSettings.Value;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            var model = new CheckoutViewModel
            {
                IdempotencyKey = Guid.NewGuid().ToString("N")
            };

            var user = await _context.Users.AsNoTracking().SingleOrDefaultAsync(item => item.Id == userId);
            if (user != null)
            {
                model.Name = user.FullName;
                model.Phone = user.PhoneNumber ?? string.Empty;
            }

            await PopulateCheckoutAsync(model, userId);
            if (model.CartItems.Count == 0)
            {
                return RedirectToAction("Index", "Cart");
            }

            var selectedAddress = model.UserAddresses.FirstOrDefault(address => address.IsDefault) ??
                                  model.UserAddresses.FirstOrDefault();
            if (selectedAddress != null)
            {
                model.SelectedAddressId = selectedAddress.Id;
                model.City = selectedAddress.City;
                model.Area = selectedAddress.Area;
                model.Street = selectedAddress.Street;
                model.Building = selectedAddress.Building;
                model.Notes = selectedAddress.Notes;
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(CheckoutViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Challenge();
            }

            model.IdempotencyKey = model.IdempotencyKey?.Trim() ?? string.Empty;
            if (!ModelState.IsValid)
            {
                await PopulateCheckoutAsync(model, userId);
                return View("Index", model);
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            CheckoutAttemptResult attempt;

            try
            {
                attempt = await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction =
                        await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

                    var existingOrder = await _context.Orders
                        .AsNoTracking()
                        .SingleOrDefaultAsync(order =>
                            order.UserId == userId &&
                            order.IdempotencyKey == model.IdempotencyKey);

                    if (existingOrder != null)
                    {
                        await transaction.CommitAsync();
                        return CheckoutAttemptResult.Completed(existingOrder.Id);
                    }

                    var cart = await _context.Carts
                        .SingleOrDefaultAsync(item => item.UserId == userId);
                    var cartItems = cart == null
                        ? []
                        : await _context.DbCartItems
                            .Where(item => item.CartId == cart.Id)
                            .OrderBy(item => item.Id)
                            .ToListAsync();

                    if (cartItems.Count == 0)
                    {
                        return CheckoutAttemptResult.Invalid("سلة التسوق فارغة.");
                    }

                    if (cartItems.Sum(item => item.Quantity) > CommerceLimits.MaxTotalCartQuantity)
                    {
                        return CheckoutAttemptResult.Invalid(
                            $"الحد الأقصى لإجمالي عناصر السلة هو {CommerceLimits.MaxTotalCartQuantity}.");
                    }

                    var productIds = cartItems.Select(item => item.ProductId).Distinct().ToList();
                    var products = await _context.Products
                        .Include(product => product.WeightTiers)
                        .Where(product => productIds.Contains(product.Id))
                        .ToDictionaryAsync(product => product.Id);

                    var errors = new List<string>();
                    var orderItems = new List<OrderItem>();
                    var subtotal = 0m;
                    var pricesChanged = false;

                    foreach (var cartItem in cartItems)
                    {
                        if (cartItem.Quantity is < 1 or > CommerceLimits.MaxQuantityPerLine)
                        {
                            errors.Add($"كمية المنتج {cartItem.ProductNameSnapshot} غير صحيحة.");
                            continue;
                        }

                        if (!products.TryGetValue(cartItem.ProductId, out var product))
                        {
                            errors.Add($"المنتج {cartItem.ProductNameSnapshot} لم يعد متاحًا.");
                            continue;
                        }

                        var currentPrice = _pricingService.Calculate(
                            product,
                            cartItem.SelectedWeightKg,
                            cartItem.CuttingSelected);

                        if (!currentPrice.IsValid)
                        {
                            errors.Add($"{product.Name}: {currentPrice.ErrorMessage}");
                            continue;
                        }

                        if (cartItem.UnitPriceSnapshot != currentPrice.UnitPrice ||
                            cartItem.SelectedPricePerKg != currentPrice.SelectedPricePerKg ||
                            cartItem.CuttingFeeApplied != currentPrice.CuttingFeeApplied)
                        {
                            cartItem.UnitPriceSnapshot = currentPrice.UnitPrice;
                            cartItem.SelectedPricePerKg = currentPrice.SelectedPricePerKg;
                            cartItem.CuttingSelected = currentPrice.CuttingSelected;
                            cartItem.CuttingFeeApplied = currentPrice.CuttingFeeApplied;
                            pricesChanged = true;
                        }

                        cartItem.ProductNameSnapshot = product.Name;
                        cartItem.ImageUrlSnapshot = product.ImageUrl;

                        var lineTotal = checked(currentPrice.UnitPrice * cartItem.Quantity);
                        subtotal = checked(subtotal + lineTotal);
                        orderItems.Add(new OrderItem
                        {
                            ProductId = product.Id,
                            ProductName = product.Name,
                            UnitPrice = currentPrice.UnitPrice,
                            Quantity = cartItem.Quantity,
                            ImageUrl = product.ImageUrl,
                            LineTotal = lineTotal,
                            SelectedWeightKg = currentPrice.SelectedWeightKg,
                            SelectedPricePerKg = currentPrice.SelectedPricePerKg,
                            CuttingSelected = currentPrice.CuttingSelected,
                            CuttingFeeApplied = currentPrice.CuttingFeeApplied
                        });
                    }

                    if (errors.Count > 0)
                    {
                        return new CheckoutAttemptResult(null, errors);
                    }

                    if (pricesChanged)
                    {
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                        return CheckoutAttemptResult.Invalid(
                            "تم تحديث أسعار بعض المنتجات. راجع الإجمالي ثم أكد الطلب مرة أخرى.");
                    }

                    var order = new Order
                    {
                        UserId = userId,
                        FullName = model.Name.Trim(),
                        Phone = model.Phone.Trim(),
                        City = model.City.Trim(),
                        Area = model.Area.Trim(),
                        Street = model.Street.Trim(),
                        Building = model.Building.Trim(),
                        Notes = model.Notes?.Trim() ?? string.Empty,
                        Subtotal = subtotal,
                        DeliveryFee = _shopSettings.DeliveryFee,
                        Total = checked(subtotal + _shopSettings.DeliveryFee),
                        CreatedAt = DateTime.UtcNow,
                        IdempotencyKey = model.IdempotencyKey,
                        OrderItems = orderItems
                    };

                    _context.Orders.Add(order);
                    _notificationService.AddOrderNotifications(order, userId);
                    _context.DbCartItems.RemoveRange(cartItems);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return CheckoutAttemptResult.Completed(order.Id);
                });
            }
            catch (DbUpdateException)
            {
                _context.ChangeTracker.Clear();
                var existingOrder = await _context.Orders
                    .AsNoTracking()
                    .SingleOrDefaultAsync(order =>
                        order.UserId == userId &&
                        order.IdempotencyKey == model.IdempotencyKey);

                if (existingOrder == null)
                {
                    throw;
                }

                attempt = CheckoutAttemptResult.Completed(existingOrder.Id);
            }

            if (attempt.OrderId.HasValue)
            {
                return RedirectToAction(nameof(Success), new { id = attempt.OrderId.Value });
            }

            foreach (var error in attempt.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            await PopulateCheckoutAsync(model, userId);
            if (model.CartItems.Count == 0)
            {
                return RedirectToAction("Index", "Cart");
            }

            return View("Index", model);
        }

        [HttpGet]
        public async Task<IActionResult> Success(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var order = await _context.Orders
                .AsNoTracking()
                .Include(item => item.OrderItems)
                .SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            return View(new OrderSuccessViewModel
            {
                OrderId = order.Id,
                OrderDate = order.CreatedAt,
                CustomerName = order.FullName,
                CustomerPhone = order.Phone,
                City = order.City,
                Area = order.Area,
                Street = order.Street,
                Building = order.Building,
                Notes = order.Notes,
                Subtotal = order.Subtotal,
                DeliveryFee = order.DeliveryFee,
                Total = order.Total,
                Items = order.OrderItems.Select(item => new OrderItemSuccessVm
                {
                    ProductName = item.ProductName,
                    ImageUrl = item.ImageUrl,
                    UnitPrice = item.UnitPrice,
                    Quantity = item.Quantity,
                    LineTotal = item.LineTotal,
                    SelectedWeightKg = item.SelectedWeightKg,
                    SelectedPricePerKg = item.SelectedPricePerKg,
                    CuttingSelected = item.CuttingSelected,
                    CuttingFeeApplied = item.CuttingFeeApplied
                }).ToList()
            });
        }

        [HttpGet]
        public async Task<IActionResult> InvoicePdf(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var order = await _context.Orders
                .AsNoTracking()
                .Include(item => item.OrderItems)
                .SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId);

            if (order == null)
            {
                return NotFound();
            }

            var pdfBytes = _pdfService.GenerateInvoice(order);
            Response.Headers["Cache-Control"] = "private, no-store";
            return File(pdfBytes, "application/pdf", $"Invoice-{order.Id}.pdf");
        }

        private async Task PopulateCheckoutAsync(CheckoutViewModel model, string userId)
        {
            var cart = await _context.Carts.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId);
            var items = cart == null
                ? []
                : await _context.DbCartItems
                    .AsNoTracking()
                    .Where(item => item.CartId == cart.Id)
                    .OrderBy(item => item.Id)
                    .ToListAsync();

            model.CartItems = items.Select(item => new CartItem
            {
                ProductId = item.ProductId,
                ProductName = item.ProductNameSnapshot,
                Price = item.UnitPriceSnapshot,
                Quantity = item.Quantity,
                ImageUrl = item.ImageUrlSnapshot,
                SelectedWeightKg = item.SelectedWeightKg,
                SelectedPricePerKg = item.SelectedPricePerKg,
                CuttingSelected = item.CuttingSelected,
                CuttingFeeApplied = item.CuttingFeeApplied
            }).ToList();

            model.UserAddresses = await _context.Addresses
                .AsNoTracking()
                .Where(address => address.UserId == userId)
                .OrderByDescending(address => address.IsDefault)
                .ThenBy(address => address.Id)
                .ToListAsync();

            model.DeliveryFee = _shopSettings.DeliveryFee;
        }

        private sealed record CheckoutAttemptResult(int? OrderId, IReadOnlyList<string> Errors)
        {
            public static CheckoutAttemptResult Completed(int orderId) => new(orderId, []);
            public static CheckoutAttemptResult Invalid(string error) => new(null, [error]);
        }
    }
}
