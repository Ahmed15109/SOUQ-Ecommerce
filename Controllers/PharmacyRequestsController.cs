using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using EcommerceApp.Data;
using EcommerceApp.Models;
using EcommerceApp.ViewModels;

namespace EcommerceApp.Controllers
{
    public class PharmacyRequestsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public PharmacyRequestsController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new PharmacyRequestVM
            {
                Medicines = new List<MedicineRowVM> { new MedicineRowVM() }
            };

            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await _context.Users
                    .Include(u => u.Addresses)
                    .FirstOrDefaultAsync(u => u.Id == userId);
                
                if (user != null)
                {
                    model.FullName = user.FullName;
                    model.UserPhone = user.PhoneNumber;
                    
                    var defaultAddress = user.Addresses.FirstOrDefault(a => a.IsDefault) ?? user.Addresses.FirstOrDefault();
                    if (defaultAddress != null)
                    {
                        model.Address = defaultAddress.FullAddress;
                    }
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PharmacyRequestVM model)
        {
            var validMedicines = model.Medicines?
                .Where(m => !string.IsNullOrWhiteSpace(m.Name))
                .ToList() ?? new List<MedicineRowVM>();

            bool hasMedicines = validMedicines.Any();
            bool hasImage = model.PrescriptionImage != null && model.PrescriptionImage.Length > 0;

            if (!hasMedicines && !hasImage)
            {
                ModelState.AddModelError("", "يجب إدخال اسم دواء واحد على الأقل أو رفع صورة الروشتة.");
            }

            if (!ModelState.IsValid)
            {
                if (model.Medicines == null || !model.Medicines.Any())
                {
                    model.Medicines = new List<MedicineRowVM> { new MedicineRowVM() };
                }
                return View(model);
            }

            string? imagePath = null;
            if (hasImage)
            {
                string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "pharmacy");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.PrescriptionImage!.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await model.PrescriptionImage.CopyToAsync(fileStream);
                }
                imagePath = "/uploads/pharmacy/" + uniqueFileName;
            }

            var pharmacyRequest = new PharmacyRequest
            {
                UserId = User.Identity?.IsAuthenticated == true ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null,
                UserPhone = model.UserPhone,
                FullName = model.FullName ?? "Guest Checkout",
                Address = model.Address,
                PrescriptionImagePath = imagePath,
                Notes = model.Notes,
                CreatedAt = DateTime.Now,
                Status = PharmacyRequestStatus.New
            };

            foreach (var med in validMedicines)
            {
                if (med.Quantity.HasValue)
                {
                    pharmacyRequest.Items.Add(new PharmacyRequestItem
                    {
                        MedicineName = med.Name,
                        Quantity = med.Quantity.Value
                    });
                }
            }

            _context.PharmacyRequests.Add(pharmacyRequest);
            await _context.SaveChangesAsync();

            var notification = new Notification
            {
                Title = "طلب صيدلية جديد",
                Message = $"طلب جديد من: {pharmacyRequest.FullName} - {pharmacyRequest.UserPhone}. عدد الأدوية: {validMedicines.Count}",
                IsForAdmin = true,
                CreatedAt = DateTime.Now,
                IsRead = false,
                PharmacyRequestId = pharmacyRequest.Id 
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Success));
        }

        [HttpGet]
        public IActionResult Success()
        {
            return View();
        }

        [HttpGet]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public async Task<IActionResult> MyRequests()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var requests = await _context.PharmacyRequests
                .Include(r => r.Items)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
            return View(requests);
        }
    }
}
