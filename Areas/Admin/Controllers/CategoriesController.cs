using EcommerceApp.Data;
using EcommerceApp.Helpers;
using EcommerceApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = AppRoles.AdminOrSuperAdmin)]
    public class CategoriesController : Controller
    {
        private readonly AppDbContext _context;

        public CategoriesController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(category => category.Id)
                .ToListAsync();

            ViewBag.ProductCounts = await _context.Products
                .AsNoTracking()
                .GroupBy(product => product.CategoryId)
                .ToDictionaryAsync(group => group.Key, group => group.Count());

            return View(categories);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (!id.HasValue)
            {
                return NotFound();
            }

            var category = await _context.Categories.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id);
            if (category == null)
            {
                return NotFound();
            }

            ViewBag.Products = await _context.Products
                .AsNoTracking()
                .Where(product => product.CategoryId == id)
                .OrderBy(product => product.Name)
                .ToListAsync();

            return View(category);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("Name,IconKey,IconClass,IconColor,IconBgColor")] Category category)
        {
            category.Name = category.Name.Trim();
            if (await _context.Categories.AnyAsync(item => item.Name == category.Name))
            {
                ModelState.AddModelError(nameof(Category.Name), "يوجد قسم بهذا الاسم بالفعل.");
            }

            if (!ModelState.IsValid)
            {
                return View(category);
            }

            _context.Categories.Add(category);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                _context.Entry(category).State = EntityState.Detached;
                if (!await _context.Categories.AsNoTracking().AnyAsync(item => item.Name == category.Name))
                {
                    throw;
                }

                ModelState.AddModelError(nameof(Category.Name), "يوجد قسم بهذا الاسم بالفعل.");
                return View(category);
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (!id.HasValue)
            {
                return NotFound();
            }

            var category = await _context.Categories.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id);
            return category == null ? NotFound() : View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("Id,Name,IconKey,IconClass,IconColor,IconBgColor,RowVersion")] Category input)
        {
            if (id != input.Id)
            {
                return NotFound();
            }

            var category = await _context.Categories.SingleOrDefaultAsync(item => item.Id == id);
            if (category == null)
            {
                return NotFound();
            }

            input.Name = input.Name.Trim();
            if (await _context.Categories.AnyAsync(item => item.Id != id && item.Name == input.Name))
            {
                ModelState.AddModelError(nameof(Category.Name), "يوجد قسم بهذا الاسم بالفعل.");
            }

            if (!ModelState.IsValid)
            {
                input.IsCore = category.IsCore;
                return View(input);
            }

            _context.Entry(category).Property(item => item.RowVersion).OriginalValue = input.RowVersion;
            category.Name = input.Name;

            if (!category.IsCore)
            {
                category.IconKey = input.IconKey;
                category.IconClass = input.IconClass;
                category.IconColor = input.IconColor;
                category.IconBgColor = input.IconBgColor;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError(string.Empty, "تم تعديل القسم بواسطة مستخدم آخر. أعد تحميل الصفحة وحاول مرة أخرى.");
                input.IsCore = category.IsCore;
                input.RowVersion = (await _context.Entry(category).GetDatabaseValuesAsync())?
                    .GetValue<byte[]>(nameof(Category.RowVersion)) ?? [];
                return View(input);
            }
            catch (DbUpdateException)
            {
                if (!await _context.Categories.AsNoTracking()
                        .AnyAsync(item => item.Id != id && item.Name == input.Name))
                {
                    throw;
                }

                ModelState.AddModelError(nameof(Category.Name), "يوجد قسم بهذا الاسم بالفعل.");
                input.IsCore = category.IsCore;
                return View(input);
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (!id.HasValue)
            {
                return NotFound();
            }

            var category = await _context.Categories.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id);
            return category == null ? NotFound() : View(category);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var category = await _context.Categories.SingleOrDefaultAsync(item => item.Id == id);
            if (category == null)
            {
                return RedirectToAction(nameof(Index));
            }

            if (category.IsCore)
            {
                TempData["Error"] = "لا يمكن حذف قسم أساسي.";
                return RedirectToAction(nameof(Index));
            }

            if (await _context.Products.AnyAsync(product => product.CategoryId == id))
            {
                TempData["Error"] = "لا يمكن حذف قسم يحتوي على منتجات.";
                return RedirectToAction(nameof(Index));
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
