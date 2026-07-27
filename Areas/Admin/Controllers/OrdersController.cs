using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcommerceApp.Data;
using EcommerceApp.Helpers;
using EcommerceApp.Models;
using EcommerceApp.Services;

using EcommerceApp.Extensions;

namespace EcommerceApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = AppRoles.AdminOrSuperAdmin)]
    public class OrdersController : Controller
    {
        private readonly AppDbContext _context;
        private readonly INotificationService _notificationService;

        public OrdersController(AppDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Index(OrderStatus? status, int page = 1, int pageSize = 20)
        {
            var query = _context.Orders.AsNoTracking().AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(o => o.Status == status.Value);
            }

            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .ThenByDescending(o => o.Id)
                .ToPagedListAsync(page, pageSize, defaultPageSize: 20, maxPageSize: 100);

            ViewBag.CurrentStatus = status;
            return View(orders);
        }

        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.OrderItems)
                .Include(o => o.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (order == null)
            {
                return NotFound();
            }
            

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, OrderStatus status, byte[]? rowVersion)
        {
            if (!Enum.IsDefined(status) || rowVersion is null || rowVersion.Length == 0)
            {
                return BadRequest();
            }

            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return NotFound();
            }

            if (!CanTransition(order.Status, status))
            {
                TempData["Error"] = "انتقال حالة الطلب غير مسموح.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (order.Status == status)
            {
                return RedirectToAction(nameof(Details), new { id });
            }

            order.Status = status;
            _context.Entry(order).Property(item => item.RowVersion).OriginalValue = rowVersion;
            _notificationService.AddOrderStatusNotification(order);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                TempData["Error"] = "تم تحديث الطلب بواسطة مسؤول آخر. راجع الحالة الحالية وحاول مرة أخرى.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        private static bool CanTransition(OrderStatus current, OrderStatus next) =>
            current == next ||
            current switch
            {
                OrderStatus.Pending => next is OrderStatus.Processing or OrderStatus.Canceled,
                OrderStatus.Processing => next is OrderStatus.Shipped or OrderStatus.Canceled,
                OrderStatus.Shipped => next == OrderStatus.Delivered,
                _ => false
            };
    }
}
