
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
        public DbSet<ProductWeightTier> ProductWeightTiers { get; set; }
        public DbSet<UserFavorite> UserFavorites { get; set; }
        public DbSet<NotificationRead> NotificationReads { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); 

            modelBuilder.Entity<Order>()
                .HasIndex(o => new { o.UserId, o.IdempotencyKey })
                .IsUnique()
                .HasFilter("[IdempotencyKey] IS NOT NULL");

            modelBuilder.Entity<Cart>()
                .HasIndex(c => c.UserId)
                .IsUnique();

            modelBuilder.Entity<DbCartItem>()
                .HasIndex(ci => new { ci.CartId, ci.ProductId, ci.CuttingSelected })
                .HasDatabaseName("IX_DbCartItems_NormalLine")
                .IsUnique()
                .HasFilter("[SelectedWeightKg] IS NULL");

            modelBuilder.Entity<DbCartItem>()
                .HasIndex(ci => new { ci.CartId, ci.ProductId, ci.SelectedWeightKg, ci.CuttingSelected })
                .HasDatabaseName("IX_DbCartItems_WeightedLine")
                .IsUnique()
                .HasFilter("[SelectedWeightKg] IS NOT NULL");

            modelBuilder.Entity<Address>()
                .HasIndex(a => a.UserId)
                .IsUnique()
                .HasFilter("[IsDefault] = 1");

            modelBuilder.Entity<Category>()
                .HasIndex(c => c.Name)
                .IsUnique();

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProductWeightTier>()
                .HasIndex(t => new { t.ProductId, t.FromKg, t.ToKg })
                .IsUnique();

            modelBuilder.Entity<UserFavorite>()
                .HasIndex(uf => new { uf.UserId, uf.ProductId })
                .IsUnique();

            modelBuilder.Entity<UserFavorite>()
                .HasOne(uf => uf.User)
                .WithMany(u => u.UserFavorites)
                .HasForeignKey(uf => uf.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserFavorite>()
                .HasOne(uf => uf.Product)
                .WithMany(p => p.UserFavorites)
                .HasForeignKey(uf => uf.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Order>()
                .HasIndex(o => new { o.UserId, o.CreatedAt })
                .IsDescending(false, true);

            modelBuilder.Entity<Notification>()
                .HasIndex(n => new { n.UserId, n.IsForAdmin, n.IsRead, n.CreatedAt })
                .IsDescending(false, false, false, true);

            modelBuilder.Entity<Notification>()
                .HasIndex(n => new { n.IsForAdmin, n.IsRead, n.CreatedAt })
                .IsDescending(false, false, true);

            modelBuilder.Entity<PharmacyRequest>()
                .HasIndex(r => new { r.UserId, r.CreatedAt })
                .IsDescending(false, true);

            modelBuilder.Entity<PharmacyRequest>()
                .HasIndex(r => new { r.UserId, r.SubmissionToken })
                .IsUnique()
                .HasFilter("[SubmissionToken] IS NOT NULL");

            modelBuilder.Entity<NotificationRead>()
                .HasIndex(r => new { r.NotificationId, r.UserId })
                .IsUnique();

            modelBuilder.Entity<NotificationRead>()
                .HasOne(r => r.Notification)
                .WithMany(n => n.Reads)
                .HasForeignKey(r => r.NotificationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<NotificationRead>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Product>()
                .ToTable(t => t.HasCheckConstraint(
                    "CK_Products_WeightConfiguration",
                    "([SellingMode] = 0 AND [AllowCutting] = 0 AND [CuttingFee] = 0) OR ([SellingMode] = 1 AND [MinKg] > 0 AND [MaxKg] > [MinKg] AND [StepKg] > 0 AND [PricePerKg] > 0 AND [CuttingFee] >= 0)"));

            modelBuilder.Entity<ProductWeightTier>()
                .ToTable(t => t.HasCheckConstraint(
                    "CK_ProductWeightTiers_ValidRange",
                    "[FromKg] > 0 AND [ToKg] > [FromKg] AND [PricePerKg] > 0"));

            modelBuilder.Entity<DbCartItem>()
                .ToTable(t => t.HasCheckConstraint(
                    "CK_DbCartItems_Quantity",
                    "[Quantity] >= 1 AND [Quantity] <= 100"));

            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "الخضار", IsCore = true },
                new Category { Id = 2, Name = "السوبر ماركت", IsCore = true },
                new Category { Id = 3, Name = "الصيدلية", IsCore = true },
                new Category { Id = 4, Name = "المطاعم", IsCore = true },
                new Category { Id = 5, Name = "الدواجن", IsCore = true }
            );

        }
    }
}
