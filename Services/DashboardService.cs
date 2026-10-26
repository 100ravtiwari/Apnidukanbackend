using Microsoft.EntityFrameworkCore;
using Dukan.API.Data;
using Dukan.API.DTOs.Common;
using Dukan.API.DTOs.Dashboard;
using Dukan.API.DTOs.Orders;
using Dukan.API.DTOs.Products;

namespace Dukan.API.Services;

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _context;

    public DashboardService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<DashboardSummaryDto>> GetDashboardSummaryAsync()
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var totalSales = await _context.Orders
            .Where(o => o.Status != "Cancelled")
            .SumAsync(o => o.TotalAmount);

        var monthlySales = await _context.Orders
            .Where(o => o.OrderDate >= monthStart && o.Status != "Cancelled")
            .SumAsync(o => o.TotalAmount);

        var totalOrders = await _context.Orders.CountAsync();
        var monthlyOrders = await _context.Orders.CountAsync(o => o.OrderDate >= monthStart);
        var todayOrders = await _context.Orders.CountAsync(o => o.OrderDate >= todayStart);

        var totalCustomers = await _context.Users.CountAsync(u => u.Role == "Customer");
        var totalProducts = await _context.Products.CountAsync();

        var pendingOrders = await _context.Orders.CountAsync(o => o.Status == "Pending");
        var outForDeliveryOrders = await _context.Orders.CountAsync(o => o.Status == "OutForDelivery");
        var completedOrders = await _context.Orders.CountAsync(o => o.Status == "Delivered");

        // Low stock products (less than or equal to 5 items left)
        var lowStockProducts = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.StockQuantity <= 5)
            .OrderBy(p => p.StockQuantity)
            .Take(10)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                DiscountPrice = p.DiscountPrice,
                StockQuantity = p.StockQuantity,
                Unit = p.Unit,
                ImageUrl = p.ImageUrl,
                IsAvailable = p.IsAvailable,
                CategoryId = p.CategoryId,
                CategoryName = p.Category != null ? p.Category.Name : string.Empty,
                CreatedAt = p.CreatedAt
            })
            .ToListAsync();

        // Recent 5 orders
        var recentOrders = await _context.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.User)
            .OrderByDescending(o => o.OrderDate)
            .Take(5)
            .Select(o => new OrderDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                UserId = o.UserId,
                OrderDate = o.OrderDate,
                SubTotal = o.SubTotal,
                DeliveryFee = o.DeliveryFee,
                TotalAmount = o.TotalAmount,
                Status = o.Status,
                PaymentMethod = o.PaymentMethod,
                PaymentStatus = o.PaymentStatus,
                CustomerName = o.CustomerName,
                CustomerPhone = o.CustomerPhone,
                CustomerEmail = o.User != null ? o.User.Email : null,
                DeliveryAddress = o.DeliveryAddress,
                DeliveryCity = o.DeliveryCity,
                DeliveryPincode = o.DeliveryPincode,
                Notes = o.Notes,
                DeliveryOtp = o.DeliveryOtp,
                DispatchedAt = o.DispatchedAt,
                DeliveredAt = o.DeliveredAt,
                Items = o.OrderItems.Select(oi => new OrderItemDto
                {
                    Id = oi.Id,
                    ProductId = oi.ProductId,
                    ProductName = oi.ProductName,
                    UnitPrice = oi.UnitPrice,
                    Quantity = oi.Quantity,
                    TotalPrice = oi.TotalPrice
                }).ToList()
            })
            .ToListAsync();

        var summary = new DashboardSummaryDto
        {
            TotalRevenue = totalSales,
            MonthlyRevenue = monthlySales,
            TotalOrders = totalOrders,
            MonthlyOrders = monthlyOrders,
            TodayOrders = todayOrders,
            TotalCustomers = totalCustomers,
            TotalProducts = totalProducts,
            PendingOrders = pendingOrders,
            OutForDeliveryOrders = outForDeliveryOrders,
            CompletedOrders = completedOrders,
            LowStockProducts = lowStockProducts,
            RecentOrders = recentOrders
        };

        return ApiResponse<DashboardSummaryDto>.Ok(summary);
    }
}
