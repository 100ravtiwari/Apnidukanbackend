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
    Task<ApiResponse<OrderDto>> VerifyDeliveryOtpAsync(int orderId, string otp, string? paymentCollectionMode = null);
    Task<ApiResponse<OrderDto>> ConfirmDoorstepUpiPaymentAsync(int orderId, string? transactionRef = null, string? payerVpa = null);
    Task<ApiResponse<List<OrderDto>>> GetAvailableDeliveriesAsync();
    Task<ApiResponse<OrderDto>> AcceptDeliveryAsync(int orderId, int riderId);

    // script2.sql Stage 1: Order Inception & Online PG [Order].[PendingOrder]
    Task<ApiResponse<InitiatePgPaymentResponseDto>> InitiatePgPaymentAsync(int userId, InitiatePgPaymentDto dto);
    Task<ApiResponse<OrderDto>> ConfirmPgPaymentAsync(int userId, ConfirmPgPaymentDto dto);

    // script2.sql Kirana Shortage Exception & Loss Audits
    Task<ApiResponse<OrderDto>> HandleUnavailableItemAsync(int orderId, HandleUnavailableItemDto dto, string settledBy);
    Task<ApiResponse<LossLogDto>> LogLossAsync(LogLossDto dto, string loggedBy);
    Task<ApiResponse<List<LossLogDto>>> GetLossLogsAsync();
}
