using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VirtualLibrary.Models;

namespace VirtualLibrary.Data
{
    public class AppDbContext : IdentityDbContext<IdentityUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<ApplicationUser> ApplicationUsers { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<Favorite> Favorites { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Audiobook> Audiobooks { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<ApplicationUser>(entity =>
            {
                entity.HasKey(a => a.Id);

                entity.HasOne<IdentityUser>()
                    .WithOne()
                    .HasForeignKey<ApplicationUser>(a => a.IdentityUserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Product>(entity =>
            {
                entity.HasOne(p => p.Supplier)
                    .WithMany(s => s.Products)
                    .HasForeignKey(p => p.SupplierId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(p => p.Category)
                    .WithMany(c => c.Products)
                    .HasForeignKey(p => p.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(p => p.Price)
                    .HasPrecision(18, 2);
            });

            builder.Entity<OrderItem>(entity =>
            {
                entity.HasKey(oi => oi.OrderItemId);

                entity.HasOne(oi => oi.Order)
                    .WithMany(o => o.Items)
                    .HasForeignKey(oi => oi.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(oi => oi.Product)
                    .WithMany()
                    .HasForeignKey(oi => oi.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(oi => oi.Price)
                    .HasPrecision(18, 2);
            });

            builder.Entity<Order>(entity =>
            {
                entity.HasKey(o => o.OrderId);

                entity.HasOne(o => o.User)
                    .WithMany()
                    .HasForeignKey(o => o.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(o => o.TotalAmount)
                    .HasPrecision(18, 2);
            });

            builder.Entity<Favorite>(entity =>
            {
                entity.HasKey(f => f.Id);

                entity.HasOne(f => f.Product)
                    .WithMany()
                    .HasForeignKey(f => f.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(f => f.User)
                    .WithMany()
                    .HasForeignKey(f => f.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<CartItem>(entity =>
            {
                entity.HasKey(ci => ci.CartItemId);

                entity.HasOne(ci => ci.Product)
                    .WithMany()
                    .HasForeignKey(ci => ci.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(ci => ci.User)
                    .WithMany()
                    .HasForeignKey(ci => ci.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Audiobook>(entity =>
            {
                entity.HasKey(a => a.AudiobookId);

                entity.HasOne(a => a.Product)
                    .WithMany()
                    .HasForeignKey(a => a.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.Property(a => a.Status)
                    .HasMaxLength(50)
                    .HasDefaultValue("Pending");

                entity.HasIndex(a => a.ProductId);
            });
        }
    }
}