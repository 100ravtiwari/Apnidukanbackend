using Microsoft.EntityFrameworkCore;
using Dukan.API.Models;

namespace Dukan.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    // script2.sql enterprise additions
    public DbSet<VariantDetail> VariantDetails => Set<VariantDetail>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<OtpVerificationForOrder> OtpVerificationForOrders => Set<OtpVerificationForOrder>();
    public DbSet<UnavailableProductDetail> UnavailableProductDetails => Set<UnavailableProductDetail>();
    public DbSet<LossInOrderLog> LossInOrderLogs => Set<LossInOrderLog>();
    public DbSet<PendingOrder> PendingOrders => Set<PendingOrder>();
    public DbSet<UserLoginOtp> UserLoginOtps => Set<UserLoginOtp>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Email).IsRequired().HasMaxLength(150);
            entity.Property(u => u.Name).IsRequired().HasMaxLength(100);
            entity.Property(u => u.Role).IsRequired().HasMaxLength(20);
        });

        // Category
        modelBuilder.Entity<Category>(entity =>
        {
            entity.Property(c => c.Name).IsRequired().HasMaxLength(100);
        });

        // Product
        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Price).HasPrecision(18, 2);
            entity.Property(p => p.DiscountPrice).HasPrecision(18, 2);
            entity.Property(p => p.GstRate).HasPrecision(5, 2);
            entity.Property(p => p.Unit).HasMaxLength(50);

            entity.HasOne(p => p.Category)
                  .WithMany(c => c.Products)
                  .HasForeignKey(p => p.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // VariantDetail (Master Weight & Size Specifications)
        modelBuilder.Entity<VariantDetail>(entity =>
        {
            entity.ToTable("VariantDetails");
            entity.Property(vd => vd.VariantName).IsRequired().HasMaxLength(50);
            entity.Property(vd => vd.UnitValue).HasPrecision(10, 2);
            entity.Property(vd => vd.Unit).HasMaxLength(20);
        });

        // ProductVariant
        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.ToTable("ProductVariants");
            entity.Property(pv => pv.VariantName).IsRequired().HasMaxLength(100);
            entity.Property(pv => pv.Price).HasPrecision(18, 2);
            entity.Property(pv => pv.DiscountPrice).HasPrecision(18, 2);
            entity.Property(pv => pv.Unit).HasMaxLength(50);

            entity.HasOne(pv => pv.Product)
                  .WithMany(p => p.Variants)
                  .HasForeignKey(pv => pv.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pv => pv.VariantDetail)
                  .WithMany(vd => vd.ProductVariants)
                  .HasForeignKey(pv => pv.VariantDetailId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // OtpVerificationForOrder
        modelBuilder.Entity<OtpVerificationForOrder>(entity =>
        {
            entity.ToTable("OtpVerificationForOrders");
            entity.Property(o => o.Otp).IsRequired().HasMaxLength(10);
            entity.Property(o => o.MobileNumber).HasMaxLength(20);

            entity.HasOne(o => o.Order)
                  .WithMany()
                  .HasForeignKey(o => o.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // CartItem
        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasOne(ci => ci.User)
                  .WithMany(u => u.CartItems)
                  .HasForeignKey(ci => ci.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ci => ci.Product)
                  .WithMany(p => p.CartItems)
                  .HasForeignKey(ci => ci.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Order
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(o => o.OrderNumber).IsUnique();
            entity.Property(o => o.OrderNumber).IsRequired().HasMaxLength(50);
            entity.Property(o => o.SubTotal).HasPrecision(18, 2);
            entity.Property(o => o.DeliveryFee).HasPrecision(18, 2);
            entity.Property(o => o.TotalAmount).HasPrecision(18, 2);
            entity.Property(o => o.GstTotal).HasPrecision(18, 2);
            entity.Property(o => o.Status).HasMaxLength(30);
            entity.Property(o => o.PaymentMethod).HasMaxLength(30);
            entity.Property(o => o.PaymentStatus).HasMaxLength(30);

            entity.HasOne(o => o.User)
                  .WithMany(u => u.Orders)
                  .HasForeignKey(o => o.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(o => o.AssignedRider)
                  .WithMany()
                  .HasForeignKey(o => o.AssignedRiderId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // OrderItem
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.Property(oi => oi.ProductName).IsRequired().HasMaxLength(200);
            entity.Property(oi => oi.UnitPrice).HasPrecision(18, 2);
            entity.Property(oi => oi.TotalPrice).HasPrecision(18, 2);
            entity.Property(oi => oi.GstRate).HasPrecision(5, 2);
            entity.Property(oi => oi.GstAmount).HasPrecision(18, 2);
            entity.Property(oi => oi.RefundAmount).HasPrecision(18, 2);

            entity.HasOne(oi => oi.Order)
                  .WithMany(o => o.OrderItems)
                  .HasForeignKey(oi => oi.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(oi => oi.Product)
                  .WithMany(p => p.OrderItems)
                  .HasForeignKey(oi => oi.ProductId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // UnavailableProductDetail
        modelBuilder.Entity<UnavailableProductDetail>(entity =>
        {
            entity.Property(upd => upd.ProductName).IsRequired().HasMaxLength(200);
            entity.Property(upd => upd.RefundAmount).HasPrecision(18, 2);
            entity.Property(upd => upd.Action).HasMaxLength(50);

            entity.HasOne(upd => upd.Order)
                  .WithMany()
                  .HasForeignKey(upd => upd.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // LossInOrderLog
        modelBuilder.Entity<LossInOrderLog>(entity =>
        {
            entity.Property(l => l.ProductName).IsRequired().HasMaxLength(200);
            entity.Property(l => l.EstimatedLossAmount).HasPrecision(18, 2);
            entity.Property(l => l.LossType).HasMaxLength(100);
            entity.Property(l => l.LossBy).HasMaxLength(50);

            entity.HasOne(l => l.Order)
                  .WithMany()
                  .HasForeignKey(l => l.OrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Stage 1: PendingOrder ([Order].[PendingOrder])
        modelBuilder.Entity<PendingOrder>(entity =>
        {
            entity.ToTable("PendingOrders");
            entity.Property(po => po.PgPaymentOrderId).IsRequired().HasMaxLength(200);
            entity.Property(po => po.PgMid).HasMaxLength(50);
            entity.Property(po => po.Amount).HasPrecision(18, 2);
            entity.Property(po => po.Status).HasMaxLength(50);

            entity.HasOne(po => po.User)
                  .WithMany()
                  .HasForeignKey(po => po.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(po => po.Order)
                  .WithMany()
                  .HasForeignKey(po => po.OrderId)
                  .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
