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
    public class ProductWeightTiersController : Controller
    {
        private readonly AppDbContext _context;

        public ProductWeightTiersController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int productId)
        {
            var product = await _context.Products
                .AsNoTracking()
                .Include(item => item.WeightTiers)
                .SingleOrDefaultAsync(item => item.Id == productId);

            if (product == null)
            {
                return NotFound();
            }

            ViewData["Product"] = product;
            return View(product.WeightTiers.OrderBy(tier => tier.FromKg).ToList());
        }

        public async Task<IActionResult> Create(int productId)
        {
            var product = await _context.Products.AsNoTracking().SingleOrDefaultAsync(item => item.Id == productId);
            if (product == null || product.SellingMode != SellingMode.ByWeight)
            {
                return NotFound();
            }

            ViewData["Product"] = product;
            return View(new ProductWeightTier { ProductId = productId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("ProductId,FromKg,ToKg,PricePerKg")] ProductWeightTier tier)
        {
            await ValidateTierAsync(tier);
            if (!ModelState.IsValid)
            {
                ViewData["Product"] = await _context.Products.AsNoTracking()
                    .SingleOrDefaultAsync(product => product.Id == tier.ProductId);
                return View(tier);
            }

            _context.ProductWeightTiers.Add(tier);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                _context.Entry(tier).State = EntityState.Detached;
                if (!await ExactTierExistsAsync(tier))
                {
                    throw;
                }

                ModelState.AddModelError(string.Empty, "توجد شريحة بالنطاق نفسه بالفعل.");
                ViewData["Product"] = await _context.Products.AsNoTracking()
                    .SingleOrDefaultAsync(product => product.Id == tier.ProductId);
                return View(tier);
            }

            return RedirectToAction(nameof(Index), new { productId = tier.ProductId });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var tier = await _context.ProductWeightTiers
                .AsNoTracking()
                .Include(item => item.Product)
                .SingleOrDefaultAsync(item => item.Id == id);

            if (tier == null)
            {
                return NotFound();
            }

            ViewData["Product"] = tier.Product;
            return View(tier);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("Id,ProductId,FromKg,ToKg,PricePerKg,RowVersion")] ProductWeightTier input)
        {
            if (id != input.Id)
            {
                return NotFound();
            }

            var tier = await _context.ProductWeightTiers.SingleOrDefaultAsync(item => item.Id == id);
            if (tier == null || tier.ProductId != input.ProductId)
            {
                return NotFound();
            }

            await ValidateTierAsync(input, id);
            if (!ModelState.IsValid)
            {
                ViewData["Product"] = await _context.Products.AsNoTracking()
                    .SingleOrDefaultAsync(product => product.Id == input.ProductId);
                return View(input);
            }

            _context.Entry(tier).Property(item => item.RowVersion).OriginalValue = input.RowVersion;
            tier.FromKg = input.FromKg;
            tier.ToKg = input.ToKg;
            tier.PricePerKg = input.PricePerKg;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError(string.Empty, "تم تعديل شريحة السعر بواسطة مستخدم آخر.");
                ViewData["Product"] = await _context.Products.AsNoTracking()
                    .SingleOrDefaultAsync(product => product.Id == input.ProductId);
                return View(input);
            }
            catch (DbUpdateException)
            {
                if (!await ExactTierExistsAsync(input, id))
                {
                    throw;
                }

                ModelState.AddModelError(string.Empty, "توجد شريحة بالنطاق نفسه بالفعل.");
                ViewData["Product"] = await _context.Products.AsNoTracking()
                    .SingleOrDefaultAsync(product => product.Id == input.ProductId);
                return View(input);
            }

            return RedirectToAction(nameof(Index), new { productId = input.ProductId });
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tier = await _context.ProductWeightTiers.SingleOrDefaultAsync(item => item.Id == id);
            if (tier == null)
            {
                return NotFound();
            }

            var productId = tier.ProductId;
            _context.ProductWeightTiers.Remove(tier);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { productId });
        }

        private async Task ValidateTierAsync(ProductWeightTier tier, int? excludedId = null)
        {
            var product = await _context.Products.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == tier.ProductId);

            if (product == null || product.SellingMode != SellingMode.ByWeight)
            {
                ModelState.AddModelError(nameof(ProductWeightTier.ProductId), "المنتج غير موجود أو لا يباع بالوزن.");
                return;
            }

            if (tier.FromKg >= tier.ToKg)
            {
                ModelState.AddModelError(nameof(ProductWeightTier.ToKg), "الوزن النهائي يجب أن يزيد عن الوزن الابتدائي.");
            }

            if (product.MinKg.HasValue && tier.FromKg < product.MinKg.Value ||
                product.MaxKg.HasValue && tier.ToKg > product.MaxKg.Value)
            {
                ModelState.AddModelError(string.Empty, "شريحة السعر يجب أن تقع داخل نطاق وزن المنتج.");
            }

            var overlaps = await _context.ProductWeightTiers.AnyAsync(existing =>
                existing.ProductId == tier.ProductId &&
                existing.Id != excludedId &&
                existing.FromKg < tier.ToKg &&
                tier.FromKg < existing.ToKg);

            if (overlaps)
            {
                ModelState.AddModelError(string.Empty, "تتداخل شريحة السعر مع شريحة موجودة.");
            }
        }

        private Task<bool> ExactTierExistsAsync(ProductWeightTier tier, int? excludedId = null) =>
            _context.ProductWeightTiers.AsNoTracking().AnyAsync(existing =>
                existing.ProductId == tier.ProductId &&
                existing.Id != excludedId &&
                existing.FromKg == tier.FromKg &&
                existing.ToKg == tier.ToKg);
    }
}
