using Microsoft.AspNetCore.Mvc;
using EcommerceApp.Models;
using EcommerceApp.ViewModels;
using EcommerceApp.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using EcommerceApp.Services;


namespace EcommerceApp.Controllers
{
    [Authorize]
    public class CheckoutController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly PdfInvoiceService _pdfService;
        private readonly INotificationService _notificationService;

        public CheckoutController(AppDbContext context, IConfiguration configuration, PdfInvoiceService pdfService, INotificationService notificationService)
        {
            _context = context;
            _configuration = configuration;
            _pdfService = pdfService;
            _notificationService = notificationService;
        }

        private async Task<Cart?> GetUserCartAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return null;

            return await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);
        }

        public async Task<IActionResult> Index()
        {
            var cart = await GetUserCartAsync();
            var cartItems = cart != null 
                ? await _context.DbCartItems.Where(ci => ci.CartId == cart.Id).ToListAsync()
                : new List<DbCartItem>();

            if (cartItems.Count == 0)
            {
                return RedirectToAction("Index", "Cart");
            }

            var cartItemsForView = cartItems.Select(ci => new CartItem
            {
                ProductId = ci.ProductId,
                ProductName = ci.ProductNameSnapshot,
                Price = ci.UnitPriceSnapshot,
                ImageUrl = ci.ImageUrlSnapshot,
                Quantity = ci.Quantity
            }).ToList();

            decimal deliveryFee = _configuration.GetValue<decimal>("ShopSettings:DeliveryFee", 15);

            var viewModel = new CheckoutViewModel
            {
                CartItems = cartItemsForView,
                DeliveryFee = deliveryFee
            };

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId != null)
            {
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    if (string.IsNullOrEmpty(viewModel.Name)) viewModel.Name = user.FullName;
                    if (string.IsNullOrEmpty(viewModel.Phone)) viewModel.Phone = user.PhoneNumber ?? string.Empty;
                }

                viewModel.UserAddresses = await _context.Addresses
                    .Where(a => a.UserId == userId)
                    .OrderByDescending(a => a.IsDefault)
                    .ToListAsync();
            }

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Submit(CheckoutViewModel model)
        {
            var cart = await GetUserCartAsync();
            var cartItems = cart != null
                ? await _context.DbCartItems.Where(ci => ci.CartId == cart.Id).ToListAsync()
                : new List<DbCartItem>();

            if (cartItems.Count == 0)
            {
                return RedirectToAction("Index", "Cart");
            }

            if (!ModelState.IsValid)
            {
                model.CartItems = cartItems.Select(ci => new CartItem
                {
                    ProductId = ci.ProductId,
                    ProductName = ci.ProductNameSnapshot,
                    Price = ci.UnitPriceSnapshot,
                    ImageUrl = ci.ImageUrlSnapshot,
                    Quantity = ci.Quantity
                }).ToList();
                return View("Index", model);
            }

            decimal deliveryFee = _configuration.GetValue<decimal>("ShopSettings:DeliveryFee", 15);
            decimal subtotal = cartItems.Sum(ci => ci.UnitPriceSnapshot * ci.Quantity);
            decimal total = subtotal + deliveryFee;

            var order = new Order
            {
                UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                FullName = model.Name,
                Phone = model.Phone,
                City = model.City,
                Area = model.Area,
                Street = model.Street,
                Building = model.Building,
                Notes = model.Notes,
                Subtotal = subtotal,
                DeliveryFee = deliveryFee,
                Total = total,
                CreatedAt = DateTime.Now
            };

            foreach (var item in cartItems)
            {
                order.OrderItems.Add(new OrderItem
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductNameSnapshot,
                    UnitPrice = item.UnitPriceSnapshot,
                    Quantity = item.Quantity,
                    ImageUrl = item.ImageUrlSnapshot,
                    LineTotal = item.UnitPriceSnapshot * item.Quantity,
                    
                    SelectedWeightKg = item.SelectedWeightKg,
                    SelectedPricePerKg = item.SelectedPricePerKg,
                    CuttingSelected = item.CuttingSelected,
                    CuttingFeeApplied = item.CuttingFeeApplied
                });
            }

            _context.Orders.Add(order);
            
            if (cart != null)
            {
                _context.DbCartItems.RemoveRange(cartItems);
            }
            
            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(order.UserId))
            {
                await _notificationService.CreateOrderNotificationsAsync(order.Id, order.UserId);
            }

            return RedirectToAction("Success", new { id = order.Id });
        }

        public async Task<IActionResult> Success(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return RedirectToAction("Index", "Home");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            
            if (order.UserId != userId && !isAdmin)
            {
                return Forbid();
            }

            var viewModel = new OrderSuccessViewModel
            {
                OrderId = order.Id,
                OrderDate = order.CreatedAt,
                CustomerName = order.User?.FullName ?? order.FullName,
                CustomerPhone = order.User?.PhoneNumber ?? order.Phone,
                City = order.City,
                Area = order.Area,
                Street = order.Street,
                Building = order.Building,
                Notes = order.Notes,
                Items = order.OrderItems.Select(item => new OrderItemSuccessVm
                {
                    ProductName = item.ProductName,
                    ImageUrl = item.ImageUrl,
                    UnitPrice = item.UnitPrice,
                    Quantity = item.Quantity,
                    LineTotal = item.LineTotal
                }).ToList(),
                Subtotal = order.Subtotal,
                DeliveryFee = order.DeliveryFee,
                Total = order.Total
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> InvoicePdf(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");
            
            if (order.UserId != userId && !isAdmin)
            {
                return Forbid();
            }

            var pdfBytes = _pdfService.GenerateInvoice(order);
            
            return File(pdfBytes, "application/pdf", $"Invoice-{order.Id}.pdf");
        }
    }
}
