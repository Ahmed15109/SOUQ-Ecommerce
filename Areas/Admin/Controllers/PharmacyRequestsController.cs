using EcommerceApp.Data;
using EcommerceApp.Extensions;
using EcommerceApp.Helpers;
using EcommerceApp.Models;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = AppRoles.AdminOrSuperAdmin)]
    public class PharmacyRequestsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly INotificationService _notificationService;

        public PharmacyRequestsController(
            AppDbContext context,
            INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
        {
            var requests = await _context.PharmacyRequests
                .AsNoTracking()
                .Include(request => request.Items)
                .OrderByDescending(request => request.CreatedAt)
                .ThenByDescending(request => request.Id)
                .ToPagedListAsync(page, pageSize, defaultPageSize: 20, maxPageSize: 100);

            return View(requests);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (!id.HasValue)
            {
                return NotFound();
            }

            var request = await _context.PharmacyRequests
                .AsNoTracking()
                .Include(item => item.Items)
                .SingleOrDefaultAsync(item => item.Id == id);

            return request == null ? NotFound() : View(request);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, PharmacyRequestStatus status, byte[]? rowVersion)
        {
            if (!Enum.IsDefined(status) || rowVersion is null || rowVersion.Length == 0)
            {
                return BadRequest();
            }

            var request = await _context.PharmacyRequests.SingleOrDefaultAsync(item => item.Id == id);
            if (request == null)
            {
                return NotFound();
            }

            if (!CanTransition(request.Status, status))
            {
                TempData["Error"] = "انتقال حالة الطلب غير مسموح.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (request.Status == status)
            {
                return RedirectToAction(nameof(Details), new { id });
            }

            request.Status = status;
            _context.Entry(request).Property(item => item.RowVersion).OriginalValue = rowVersion;
            _notificationService.AddPharmacyStatusNotification(request);

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

        private static bool CanTransition(PharmacyRequestStatus current, PharmacyRequestStatus next) =>
            current == next ||
            current switch
            {
                PharmacyRequestStatus.New => next is PharmacyRequestStatus.Processing or PharmacyRequestStatus.Cancelled,
                PharmacyRequestStatus.Processing => next is PharmacyRequestStatus.Shipped or PharmacyRequestStatus.Cancelled,
                PharmacyRequestStatus.Shipped => next == PharmacyRequestStatus.Delivered,
                _ => false
            };
    }
}
