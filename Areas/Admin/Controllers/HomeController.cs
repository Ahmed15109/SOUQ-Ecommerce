using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcommerceApp.Data;

namespace EcommerceApp.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,SuperAdmin")]
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
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .ToListAsync();

            ViewData["LatestProducts"] = await _context.Products
                .Include(p => p.Category)
                .OrderByDescending(p => p.Id) 
                .Take(5)
                .ToListAsync();

            return View();
        }
    }
}
