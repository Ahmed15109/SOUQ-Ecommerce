using EcommerceApp.Data;
using EcommerceApp.Constants;
using EcommerceApp.Extensions;
using EcommerceApp.Helpers;
using EcommerceApp.Models;
using EcommerceApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = AppRoles.AdminOrSuperAdmin)]
    public class ProductsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IFileUploadService _fileUploadService;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(
            AppDbContext context,
            IFileUploadService fileUploadService,
            ILogger<ProductsController> logger)
        {
            _context = context;
            _fileUploadService = fileUploadService;
            _logger = logger;
        }

        public async Task<IActionResult> Index(int? categoryId, int page = 1, int pageSize = 20)
        {
            var query = _context.Products.AsNoTracking().Include(product => product.Category).AsQueryable();
            if (categoryId.HasValue)
            {
                query = query.Where(product => product.CategoryId == categoryId.Value);
                ViewData["CurrentCategory"] = categoryId;
            }

            await SetCategoryListsAsync(categoryId);
            var products = await query
                .OrderByDescending(product => product.Id)
                .ToPagedListAsync(page, pageSize, defaultPageSize: 20, maxPageSize: 100);

            return View(products);
        }

        public async Task<IActionResult> Create(int? categoryId)
        {
            await SetCategoryListsAsync(categoryId);
            return View(new Product { CategoryId = categoryId ?? 0 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("uploads")]
        [RequestSizeLimit(CommerceLimits.MaxUploadRequestSizeBytes)]
        public async Task<IActionResult> Create(
            [Bind("Name,Description,Price,CategoryId,IsFeatured,SellingMode,MinKg,MaxKg,StepKg,AllowCutting,CuttingFee,PricePerKg")] Product product,
            IFormFile? imageFile)
        {
            NormalizeProduct(product);
            await ValidateCategoryAndModeAsync(product);

            string? newImageUrl = null;
            if (ModelState.IsValid && imageFile != null)
            {
                var upload = await _fileUploadService.SaveImageAsync(imageFile, "products");
                if (!upload.IsValid)
                {
                    ModelState.AddModelError(nameof(Product.ImageUrl), upload.ErrorMessage ?? "تعذر تحميل الصورة.");
                }
                else
                {
                    newImageUrl = upload.FilePath;
                }
            }

            if (!ModelState.IsValid)
            {
                await SetCategoryListsAsync(product.CategoryId);
                return View(product);
            }

            product.ImageUrl = newImageUrl ?? "/img/placeholder.png";
            try
            {
                _context.Products.Add(product);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                if (newImageUrl != null)
                {
                    _fileUploadService.DeleteFile(newImageUrl);
                }

                throw;
            }
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (!id.HasValue)
            {
                return NotFound();
            }

            var product = await _context.Products.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id);
            if (product == null)
            {
                return NotFound();
            }

            await SetCategoryListsAsync(product.CategoryId);
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("uploads")]
        [RequestSizeLimit(CommerceLimits.MaxUploadRequestSizeBytes)]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("Id,Name,Description,Price,CategoryId,IsFeatured,SellingMode,MinKg,MaxKg,StepKg,AllowCutting,CuttingFee,PricePerKg,RowVersion")] Product input,
            IFormFile? imageFile)
        {
            if (id != input.Id)
            {
                return NotFound();
            }

            var product = await _context.Products.SingleOrDefaultAsync(item => item.Id == id);
            if (product == null)
            {
                return NotFound();
            }

            NormalizeProduct(input);
            await ValidateCategoryAndModeAsync(input);
            input.ImageUrl = product.ImageUrl;

            string? newImageUrl = null;
            if (ModelState.IsValid && imageFile != null)
            {
                var upload = await _fileUploadService.SaveImageAsync(imageFile, "products");
                if (!upload.IsValid)
                {
                    ModelState.AddModelError(nameof(Product.ImageUrl), upload.ErrorMessage ?? "تعذر تحميل الصورة.");
                }
                else
                {
                    newImageUrl = upload.FilePath;
                }
            }

            if (!ModelState.IsValid)
            {
                await SetCategoryListsAsync(input.CategoryId);
                return View(input);
            }

            var oldImageUrl = product.ImageUrl;
            _context.Entry(product).Property(item => item.RowVersion).OriginalValue = input.RowVersion;

            product.Name = input.Name.Trim();
            product.Description = input.Description.Trim();
            product.Price = input.Price;
            product.CategoryId = input.CategoryId;
            product.IsFeatured = input.IsFeatured;
            product.SellingMode = input.SellingMode;
            product.MinKg = input.MinKg;
            product.MaxKg = input.MaxKg;
            product.StepKg = input.StepKg;
            product.AllowCutting = input.AllowCutting;
            product.CuttingFee = input.CuttingFee;
            product.PricePerKg = input.PricePerKg;
            product.ImageUrl = newImageUrl ?? oldImageUrl;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (newImageUrl != null)
                {
                    _fileUploadService.DeleteFile(newImageUrl);
                }

                ModelState.AddModelError(string.Empty, "تم تعديل المنتج بواسطة مستخدم آخر. أعد تحميل الصفحة وحاول مرة أخرى.");
                var databaseValues = await _context.Entry(product).GetDatabaseValuesAsync();
                if (databaseValues == null)
                {
                    return NotFound();
                }

                input.RowVersion = databaseValues.GetValue<byte[]>(nameof(Product.RowVersion));
                input.ImageUrl = databaseValues.GetValue<string>(nameof(Product.ImageUrl));
                await SetCategoryListsAsync(input.CategoryId);
                return View(input);
            }
            catch
            {
                if (newImageUrl != null)
                {
                    _fileUploadService.DeleteFile(newImageUrl);
                }

                throw;
            }

            if (newImageUrl != null &&
                oldImageUrl.StartsWith("/uploads/products/", StringComparison.OrdinalIgnoreCase) &&
                !_fileUploadService.DeleteFile(oldImageUrl))
            {
                _logger.LogWarning("The replaced product image {ImageUrl} could not be removed.", oldImageUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (!id.HasValue)
            {
                return NotFound();
            }

            var product = await _context.Products.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id);
            return product == null ? NotFound() : View(product);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.SingleOrDefaultAsync(item => item.Id == id);
            if (product == null)
            {
                return RedirectToAction(nameof(Index));
            }

            var imageUrl = product.ImageUrl;
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            if (imageUrl.StartsWith("/uploads/products/", StringComparison.OrdinalIgnoreCase) &&
                !_fileUploadService.DeleteFile(imageUrl))
            {
                _logger.LogWarning("The deleted product image {ImageUrl} could not be removed.", imageUrl);
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task ValidateCategoryAndModeAsync(Product product)
        {
            if (!Enum.IsDefined(product.SellingMode))
            {
                ModelState.AddModelError(nameof(Product.SellingMode), "طريقة البيع غير صحيحة.");
            }

            if (!await _context.Categories.AnyAsync(category => category.Id == product.CategoryId))
            {
                ModelState.AddModelError(nameof(Product.CategoryId), "القسم المحدد غير موجود.");
            }
        }

        private static void NormalizeProduct(Product product)
        {
            product.Name = product.Name.Trim();
            product.Description = product.Description?.Trim() ?? string.Empty;

            if (product.SellingMode == SellingMode.Normal)
            {
                product.MinKg = null;
                product.MaxKg = null;
                product.StepKg = null;
                product.AllowCutting = false;
                product.CuttingFee = 0;
                product.PricePerKg = 0;
            }
            else
            {
                product.Price = 0;
                if (!product.AllowCutting)
                {
                    product.CuttingFee = 0;
                }
            }
        }

        private async Task SetCategoryListsAsync(int? selectedCategoryId)
        {
            var categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(category => category.Name)
                .ToListAsync();

            ViewData["Categories"] = new SelectList(categories, "Id", "Name", selectedCategoryId);
            ViewData["CategoryId"] = new SelectList(categories, "Id", "Name", selectedCategoryId);
        }
    }
}
