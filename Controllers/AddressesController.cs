using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcommerceApp.Data;
using EcommerceApp.Models;
using System.Data;

namespace EcommerceApp.Controllers
{
    [Authorize]
    public class AddressesController : Controller
    {
        private readonly AppDbContext _context;

        public AddressesController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            var addresses = await _context.Addresses
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.IsDefault)
                .ToListAsync();

            return View(addresses);
        }

        public IActionResult Create(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

   
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("City,Area,Street,Building,Notes,IsDefault")] Address address, string? returnUrl = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                 return RedirectToAction("Login", "Account");
            }

            address.UserId = userId;
            
            ModelState.Remove("User");
            ModelState.Remove("UserId");

            if (ModelState.IsValid)
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction =
                        await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

                    if (address.IsDefault)
                    {
                        await _context.Addresses
                            .Where(item => item.UserId == userId && item.IsDefault)
                            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsDefault, false));
                    }
                    else if (!await _context.Addresses.AnyAsync(item => item.UserId == userId))
                    {
                        address.IsDefault = true;
                    }

                    _context.Add(address);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                });

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return LocalRedirect(returnUrl);
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.ReturnUrl = returnUrl;
            return View(address);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDefault(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var address = await _context.Addresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (address == null)
            {
                return NotFound();
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction =
                    await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

                await _context.Addresses
                    .Where(item => item.UserId == userId && item.IsDefault && item.Id != id)
                    .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsDefault, false));

                address.IsDefault = true;
                _context.Entry(address).Property(item => item.IsDefault).IsModified = true;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            });
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var address = await _context.Addresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

            if (address != null)
            {
                var wasDefault = address.IsDefault;
                var strategy = _context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction =
                        await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

                    _context.Addresses.Remove(address);
                    await _context.SaveChangesAsync();

                    if (wasDefault)
                    {
                        var replacement = await _context.Addresses
                            .Where(item => item.UserId == userId)
                            .OrderBy(item => item.Id)
                            .FirstOrDefaultAsync();

                        if (replacement != null)
                        {
                            replacement.IsDefault = true;
                            await _context.SaveChangesAsync();
                        }
                    }

                    await transaction.CommitAsync();
                });
            }
            
            return RedirectToAction(nameof(Index));
        }
    }
}
