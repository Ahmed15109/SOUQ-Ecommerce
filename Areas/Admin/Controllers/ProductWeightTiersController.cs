using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcommerceApp.Data;
using EcommerceApp.Models;

namespace EcommerceApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,SuperAdmin")]
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
                .Include(p => p.WeightTiers)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null) return NotFound();

            ViewData["Product"] = product;
            return View(product.WeightTiers.OrderBy(t => t.FromKg).ToList());
        }

        public async Task<IActionResult> Create(int productId)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null) return NotFound();

            ViewData["Product"] = product;
            return View(new ProductWeightTier { ProductId = productId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ProductId,FromKg,ToKg,PricePerKg")] ProductWeightTier tier)
        {
            if (tier.FromKg >= tier.ToKg)
            {
                ModelState.AddModelError("ToKg", "الوزن النهائي يجب أن يكون أكبر من الوزن الابتدائي");
            }

            var hasOverlap = await _context.ProductWeightTiers
                .AnyAsync(t => t.ProductId == tier.ProductId && 
                               ((tier.FromKg >= t.FromKg && tier.FromKg < t.ToKg) || 
                                (tier.ToKg > t.FromKg && tier.ToKg <= t.ToKg) ||
                                (tier.FromKg <= t.FromKg && tier.ToKg >= t.ToKg)));

            if (hasOverlap)
            {
                ModelState.AddModelError("", "هناك تداخل مع شريحة أخرى موجودة");
            }

            if (ModelState.IsValid)
            {
                _context.Add(tier);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { productId = tier.ProductId });
            }

            var product = await _context.Products.FindAsync(tier.ProductId);
            ViewData["Product"] = product;
            return View(tier);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var tier = await _context.ProductWeightTiers
                .Include(t=>t.Product)
                .FirstOrDefaultAsync(t => t.Id == id);
                
            if (tier == null) return NotFound();

            ViewData["Product"] = tier.Product;
            return View(tier);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,ProductId,FromKg,ToKg,PricePerKg")] ProductWeightTier tier)
        {
            if (id != tier.Id) return NotFound();

            if (tier.FromKg >= tier.ToKg)
            {
                ModelState.AddModelError("ToKg", "الوزن النهائي يجب أن يكون أكبر من الوزن الابتدائي");
            }

            var hasOverlap = await _context.ProductWeightTiers
                .AnyAsync(t => t.ProductId == tier.ProductId && t.Id != tier.Id &&
                               ((tier.FromKg >= t.FromKg && tier.FromKg < t.ToKg) || 
                                (tier.ToKg > t.FromKg && tier.ToKg <= t.ToKg) ||
                                (tier.FromKg <= t.FromKg && tier.ToKg >= t.ToKg)));

            if (hasOverlap)
            {
                ModelState.AddModelError("", "هناك تداخل مع شريحة أخرى موجودة");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tier);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.ProductWeightTiers.Any(e => e.Id == tier.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index), new { productId = tier.ProductId });
            }
            
            var product = await _context.Products.FindAsync(tier.ProductId);
            ViewData["Product"] = product;
            return View(tier);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var tier = await _context.ProductWeightTiers.FindAsync(id);
            if (tier == null) return NotFound();
            
            var productId = tier.ProductId;
            _context.ProductWeightTiers.Remove(tier);
            await _context.SaveChangesAsync();
            
            return RedirectToAction(nameof(Index), new { productId = productId });
        }
    }
}
