using Dukan.API.DTOs.Cart;
using Dukan.API.DTOs.Common;

namespace Dukan.API.Services;

public interface ICartService
{
    Task<ApiResponse<CartSummaryDto>> GetCartAsync(int userId);
    Task<ApiResponse<CartSummaryDto>> AddToCartAsync(int userId, AddToCartDto dto);
    Task<ApiResponse<CartSummaryDto>> UpdateQuantityAsync(int userId, int productId, int quantity);
    Task<ApiResponse<CartSummaryDto>> RemoveFromCartAsync(int userId, int productId);
    Task<ApiResponse<bool>> ClearCartAsync(int userId);
}
