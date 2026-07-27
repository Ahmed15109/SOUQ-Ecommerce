using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcommerceApp.Data;
using EcommerceApp.Helpers;

namespace EcommerceApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = AppRoles.AdminOrSuperAdmin)]
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewData["CategoriesCount"] = await _context.Categories.CountAsync();
            ViewData["ProductsCount"] = await _context.Products.CountAsync();
            ViewData["OrdersCount"] = await _context.Orders.CountAsync();
            ViewData["PharmacyRequestsCount"] = await _context.PharmacyRequests.CountAsync();

            ViewData["LatestOrders"] = await _context.Orders
                .AsNoTracking()
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .ToListAsync();

            ViewData["LatestProducts"] = await _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .OrderByDescending(p => p.Id)
                .Take(5)
                .ToListAsync();

            return View();
        }
    }
}
