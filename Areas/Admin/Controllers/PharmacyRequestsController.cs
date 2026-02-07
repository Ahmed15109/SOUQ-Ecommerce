using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcommerceApp.Data;
using System.Linq;
using System.Threading.Tasks;

namespace EcommerceApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class PharmacyRequestsController : Controller
    {
        private readonly AppDbContext _context;

        public PharmacyRequestsController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var requests = await _context.PharmacyRequests
                .Include(r => r.Items) // Include Items
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
            return View(requests);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var pharmacyRequest = await _context.PharmacyRequests
                .Include(r => r.Items)
                .Include(r => r.Items)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (pharmacyRequest == null)
            {
                return NotFound();
            }

            return View(pharmacyRequest);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, EcommerceApp.Models.PharmacyRequestStatus status)
        {
             var request = await _context.PharmacyRequests.FindAsync(id);
             if (request == null)
             {
                 return NotFound();
             }

             request.Status = status;
             await _context.SaveChangesAsync();

             // Notify User if they are registered
             if (!string.IsNullOrEmpty(request.UserId))
             {
                 var notification = new EcommerceApp.Models.Notification
                 {
                     Title = "تحديث حالة الطلب",
                     Message = $"تم تحديث حالة طلبك الصيدلي #{request.Id} إلى: {status}",
                     UserId = request.UserId,
                     IsForAdmin = false,
                     IsRead = false,
                     CreatedAt = DateTime.Now,
                     PharmacyRequestId = request.Id
                 };
                 _context.Notifications.Add(notification);
                 await _context.SaveChangesAsync();
             }

             return RedirectToAction(nameof(Details), new { id = request.Id });
        }
    }
}
