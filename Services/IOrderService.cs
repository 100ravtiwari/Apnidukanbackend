using Dukan.API.DTOs.Common;
using Dukan.API.DTOs.Orders;

namespace Dukan.API.Services;

public interface IOrderService
{
    Task<ApiResponse<OrderDto>> CreateOrderAsync(int userId, CreateOrderDto dto);
    Task<ApiResponse<List<OrderDto>>> GetUserOrdersAsync(int userId);
    Task<ApiResponse<OrderDto>> GetOrderByIdAsync(int orderId, int userId, bool isAdmin);
    Task<ApiResponse<PagedResult<OrderDto>>> GetAllOrdersAsync(string? status, int pageNumber = 1, int pageSize = 20, bool maskOtp = false);
    Task<ApiResponse<OrderDto>> UpdateOrderStatusAsync(int orderId, string newStatus);
    Task<ApiResponse<OrderDto>> UpdatePaymentStatusAsync(int orderId, string paymentStatus);
    Task<ApiResponse<OrderDto>> DispatchOrderAsync(int orderId, string? notes);
    Task<ApiResponse<OrderDto>> PackOrderAsync(int orderId, string? packedBy);
    Task<ApiResponse<OrderDto>> AssignAndDispatchOrderAsync(int orderId, int riderId, string? notes);
    Task<ApiResponse<List<OrderDto>>> GetRiderOrdersAsync(int riderId, string? status = null);
    Task<ApiResponse<OrderDto>> VerifyDeliveryOtpAsync(int orderId, string otp);
    Task<ApiResponse<List<OrderDto>>> GetAvailableDeliveriesAsync();
    Task<ApiResponse<OrderDto>> AcceptDeliveryAsync(int orderId, int riderId);
}
