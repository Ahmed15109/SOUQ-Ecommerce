
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using EcommerceApp.Models;

namespace EcommerceApp.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Address> Addresses { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<DbCartItem> DbCartItems { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<PharmacyRequest> PharmacyRequests { get; set; }
        public DbSet<PharmacyRequestItem> PharmacyRequestItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); 

            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Vegetables" },
                new Category { Id = 2, Name = "Dairy" },
                new Category { Id = 3, Name = "Pharmacy" },
                new Category { Id = 4, Name = "Food" }
            );

        }
    }
}
