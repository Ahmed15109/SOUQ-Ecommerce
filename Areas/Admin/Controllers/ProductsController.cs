
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using EcommerceApp.Data;
using EcommerceApp.Models;

namespace EcommerceApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class ProductsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(AppDbContext context, IWebHostEnvironment webHostEnvironment, ILogger<ProductsController> logger)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }

        public async Task<IActionResult> Index(int? categoryId)
        {
            var productsQuery = _context.Products.Include(p => p.Category).AsQueryable();

            if (categoryId.HasValue)
            {
                productsQuery = productsQuery.Where(p => p.CategoryId == categoryId);
                ViewData["CurrentCategory"] = categoryId;
            }

            ViewData["Categories"] = new SelectList(_context.Categories, "Id", "Name", categoryId);
            return View(await productsQuery.ToListAsync());
        }

        public IActionResult Create(int? categoryId)
        {
            ViewData["Categories"] = new SelectList(_context.Categories, "Id", "Name", categoryId);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Description,Price,CategoryId,IsFavorite,SellingMode,MinKg,MaxKg,StepKg,AllowCutting,CuttingFee,PricePerKg")] Product product, IFormFile? imageFile)
        {
            
            ModelState.Remove("ImageUrl");

            if (ModelState.IsValid)
            {
                if (imageFile != null)
                {
                    product.ImageUrl = await SaveImage(imageFile);
                }
                else
                {
                    product.ImageUrl = ""; 
                }

                _context.Add(product);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            foreach (var modelState in ModelState.Values)
            {
                foreach (var error in modelState.Errors)
                {
                    _logger.LogError($"Product creation validation error: {error.ErrorMessage}");
                }
            }

            ViewData["Categories"] = new SelectList(_context.Categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description,Price,ImageUrl,CategoryId,IsFavorite,SellingMode,MinKg,MaxKg,StepKg,AllowCutting,CuttingFee,PricePerKg")] Product product, IFormFile? imageFile)
        {
            if (id != product.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    if (imageFile != null)
                    {
                        if (!string.IsNullOrEmpty(product.ImageUrl) && product.ImageUrl.StartsWith("/uploads/products/"))
                        {
                            var oldPath = Path.Combine(_webHostEnvironment.WebRootPath, product.ImageUrl.TrimStart('/'));
                            if (System.IO.File.Exists(oldPath))
                            {
                                System.IO.File.Delete(oldPath);
                            }
                        }

                        product.ImageUrl = await SaveImage(imageFile);
                    }

                    _context.Update(product);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Products.Any(e => e.Id == product.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", product.CategoryId);
            return View(product);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            return View(product);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                 if (!string.IsNullOrEmpty(product.ImageUrl) && product.ImageUrl.StartsWith("/uploads/products/"))
                {
                    var oldPath = Path.Combine(_webHostEnvironment.WebRootPath, product.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath))
                    {
                         System.IO.File.Delete(oldPath);
                    }
                }

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task<string> SaveImage(IFormFile imageFile)
        {
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "products");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            string uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(fileStream);
            }

            return "/uploads/products/" + uniqueFileName;
        }
    }
}
