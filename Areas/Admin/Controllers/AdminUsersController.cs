using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcommerceApp.Models;
using EcommerceApp.Areas.Admin.ViewModels;
using EcommerceApp.Helpers;
using EcommerceApp.Extensions;
using EcommerceApp.Data;
using System.Security.Claims;

namespace EcommerceApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public class AdminUsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AppDbContext _context;

        public AdminUsersController(UserManager<ApplicationUser> userManager, AppDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index(int page = 1, int pageSize = 20)
        {
            var adminRoleId = await _context.Roles
                .Where(role => role.Name == AppRoles.Admin)
                .Select(role => role.Id)
                .SingleOrDefaultAsync();
            var superAdminRoleId = await _context.Roles
                .Where(role => role.Name == AppRoles.SuperAdmin)
                .Select(role => role.Id)
                .SingleOrDefaultAsync();

            var pagedAdmins = await _context.Users
                .AsNoTracking()
                .Where(user => _context.UserRoles.Any(link =>
                        link.UserId == user.Id && link.RoleId == adminRoleId) &&
                    !_context.UserRoles.Any(link =>
                        link.UserId == user.Id && link.RoleId == superAdminRoleId))
                .OrderBy(u => u.Id)
                .ToPagedListAsync(page, pageSize, defaultPageSize: 20, maxPageSize: 100);
            return View(pagedAdmins);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateAdminUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email.Trim(),
                    Email = model.Email.Trim(),
                    EmailConfirmed = true,
                    FullName = model.FullName.Trim()
                };
                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    var roleResult = await _userManager.AddToRoleAsync(user, AppRoles.Admin);
                    if (roleResult.Succeeded)
                    {
                        return RedirectToAction(nameof(Index));
                    }

                    await _userManager.DeleteAsync(user);
                    foreach (var error in roleResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLockout(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest();
            }

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (id == currentUserId)
            {
                TempData["Error"] = "لا يمكنك تعطيل حسابك الحالي.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null || !await _userManager.IsInRoleAsync(user, AppRoles.Admin))
            {
                return NotFound();
            }

            if (await _userManager.IsInRoleAsync(user, AppRoles.SuperAdmin))
            {
                return BadRequest();
            }

            var isLocked = await _userManager.IsLockedOutAsync(user);
            var result = await _userManager.SetLockoutEndDateAsync(
                user,
                isLocked ? null : DateTimeOffset.MaxValue);

            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join(" ", result.Errors.Select(error => error.Description));
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
