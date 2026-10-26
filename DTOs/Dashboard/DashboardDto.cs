using Dukan.API.DTOs.Orders;
using Dukan.API.DTOs.Products;

namespace Dukan.API.DTOs.Dashboard;

public class DashboardSummaryDto
{
    public decimal TotalRevenue { get; set; }
    public decimal MonthlyRevenue { get; set; }
    public int TotalOrders { get; set; }
    public int MonthlyOrders { get; set; }
    public int TodayOrders { get; set; }
    public int TotalCustomers { get; set; }
    public int TotalProducts { get; set; }
    public int PendingOrders { get; set; }
    public int OutForDeliveryOrders { get; set; }
    public int CompletedOrders { get; set; }
    public List<ProductDto> LowStockProducts { get; set; } = new();
    public List<OrderDto> RecentOrders { get; set; } = new();
}
