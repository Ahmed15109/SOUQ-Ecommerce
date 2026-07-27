using System.Security.Claims;
using EcommerceApp.Constants;
using EcommerceApp.Data;
using EcommerceApp.Extensions;
using EcommerceApp.Helpers;
using EcommerceApp.Models;
using EcommerceApp.Services;
using EcommerceApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Controllers
{
    public class PharmacyRequestsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IFileUploadService _fileUploadService;

        public PharmacyRequestsController(AppDbContext context, IFileUploadService fileUploadService)
        {
            _context = context;
            _fileUploadService = fileUploadService;
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new PharmacyRequestVM
            {
                Medicines = [new MedicineRowVM()],
                SubmissionToken = Guid.NewGuid().ToString("N")
            };

            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await _context.Users
                    .AsNoTracking()
                    .Include(item => item.Addresses)
                    .SingleOrDefaultAsync(item => item.Id == userId);

                if (user != null)
                {
                    model.FullName = user.FullName;
                    model.UserPhone = user.PhoneNumber ?? string.Empty;
                    var address = user.Addresses.FirstOrDefault(item => item.IsDefault) ??
                                  user.Addresses.OrderBy(item => item.Id).FirstOrDefault();
                    if (address != null)
                    {
                        model.Address = address.FullAddress;
                    }
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("uploads")]
        [RequestSizeLimit(CommerceLimits.MaxUploadRequestSizeBytes)]
        public async Task<IActionResult> Create(PharmacyRequestVM model)
        {
            model.SubmissionToken = model.SubmissionToken?.Trim() ?? string.Empty;
            model.Medicines ??= [];

            if (model.Medicines.Count > CommerceLimits.MaxPharmacyMedicines)
            {
                ModelState.AddModelError(
                    nameof(PharmacyRequestVM.Medicines),
                    $"الحد الأقصى هو {CommerceLimits.MaxPharmacyMedicines} دواءً.");
            }

            foreach (var (medicine, index) in model.Medicines.Select((value, index) => (value, index)))
            {
                var hasName = !string.IsNullOrWhiteSpace(medicine.Name);
                if (hasName && !medicine.Quantity.HasValue)
                {
                    ModelState.AddModelError($"Medicines[{index}].Quantity", "الكمية مطلوبة.");
                }
                else if (!hasName && medicine.Quantity.HasValue)
                {
                    ModelState.AddModelError($"Medicines[{index}].Name", "اسم الدواء مطلوب.");
                }
            }

            var validMedicines = model.Medicines
                .Where(medicine => !string.IsNullOrWhiteSpace(medicine.Name) && medicine.Quantity.HasValue)
                .Take(CommerceLimits.MaxPharmacyMedicines)
                .ToList();
            var hasAttachment = model.PrescriptionImage is { Length: > 0 };

            if (validMedicines.Count == 0 && !hasAttachment)
            {
                ModelState.AddModelError(string.Empty, "أدخل دواءً واحدًا على الأقل أو أرفق الروشتة.");
            }

            var currentUserId = User.Identity?.IsAuthenticated == true
                ? User.FindFirstValue(ClaimTypes.NameIdentifier)
                : null;

            if (ModelState.IsValid)
            {
                var existing = await _context.PharmacyRequests
                    .AsNoTracking()
                    .SingleOrDefaultAsync(request =>
                        request.UserId == currentUserId &&
                        request.SubmissionToken == model.SubmissionToken);

                if (existing != null)
                {
                    return RedirectToAction(nameof(Success), new { id = existing.Id });
                }
            }

            string? attachmentPath = null;
            if (ModelState.IsValid && hasAttachment)
            {
                var upload = await _fileUploadService.SavePharmacyAttachmentAsync(
                    model.PrescriptionImage,
                    HttpContext.RequestAborted);

                if (!upload.IsValid)
                {
                    ModelState.AddModelError(
                        nameof(PharmacyRequestVM.PrescriptionImage),
                        upload.ErrorMessage ?? "تعذر تحميل المرفق.");
                }
                else
                {
                    attachmentPath = upload.FilePath;
                }
            }

            if (!ModelState.IsValid)
            {
                if (attachmentPath != null)
                {
                    _fileUploadService.DeleteFile(attachmentPath);
                }

                if (model.Medicines.Count == 0)
                {
                    model.Medicines.Add(new MedicineRowVM());
                }

                return View(model);
            }

            var request = new PharmacyRequest
            {
                UserId = currentUserId,
                UserPhone = model.UserPhone.Trim(),
                FullName = model.FullName!.Trim(),
                Address = model.Address.Trim(),
                PrescriptionImagePath = attachmentPath,
                Notes = model.Notes?.Trim(),
                CreatedAt = DateTime.UtcNow,
                Status = PharmacyRequestStatus.New,
                SubmissionToken = model.SubmissionToken,
                Items = validMedicines.Select(medicine => new PharmacyRequestItem
                {
                    MedicineName = medicine.Name!.Trim(),
                    Quantity = medicine.Quantity!.Value
                }).ToList()
            };

            _context.PharmacyRequests.Add(request);
            _context.Notifications.Add(new Notification
            {
                Title = "طلب صيدلية جديد",
                Message = $"طلب صيدلية جديد من {request.FullName}. عدد الأدوية: {request.Items.Count}.",
                IsForAdmin = true,
                CreatedAt = DateTime.UtcNow,
                PharmacyRequest = request
            });

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                _context.ChangeTracker.Clear();
                var existing = await _context.PharmacyRequests
                    .AsNoTracking()
                    .SingleOrDefaultAsync(item =>
                        item.UserId == currentUserId &&
                        item.SubmissionToken == model.SubmissionToken);

                if (existing == null)
                {
                    if (attachmentPath != null)
                    {
                        _fileUploadService.DeleteFile(attachmentPath);
                    }

                    throw;
                }

                if (attachmentPath != null)
                {
                    _fileUploadService.DeleteFile(attachmentPath);
                }

                return RedirectToAction(nameof(Success), new { id = existing.Id });
            }
            catch
            {
                if (attachmentPath != null)
                {
                    _fileUploadService.DeleteFile(attachmentPath);
                }

                throw;
            }

            return RedirectToAction(nameof(Success), new { id = request.Id });
        }

        [HttpGet]
        public IActionResult Success(int? id)
        {
            ViewData["RequestId"] = id;
            return View();
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> MyRequests(int page = 1, int pageSize = 10)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var requests = await _context.PharmacyRequests
                .AsNoTracking()
                .Include(request => request.Items)
                .Where(request => request.UserId == userId)
                .OrderByDescending(request => request.CreatedAt)
                .ThenByDescending(request => request.Id)
                .ToPagedListAsync(page, pageSize, defaultPageSize: 10, maxPageSize: 50);

            return View(requests);
        }

        [HttpGet("PharmacyRequests/DownloadPrescription/{id:int}")]
        [Authorize]
        public async Task<IActionResult> DownloadPrescription(int id)
        {
            var request = await _context.PharmacyRequests.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id);
            if (request == null || string.IsNullOrWhiteSpace(request.PrescriptionImagePath))
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.SuperAdmin);
            if (!isAdmin && (request.UserId == null || request.UserId != userId))
            {
                return NotFound();
            }

            if (!_fileUploadService.TryGetPharmacyAttachment(
                    request.PrescriptionImagePath,
                    out var fullPath,
                    out var contentType,
                    out var downloadAsAttachment))
            {
                return NotFound();
            }

            Response.Headers["Cache-Control"] = "private, no-store";
            return downloadAsAttachment
                ? PhysicalFile(fullPath, contentType, Path.GetFileName(fullPath), enableRangeProcessing: true)
                : PhysicalFile(fullPath, contentType, enableRangeProcessing: true);
        }
    }
}
