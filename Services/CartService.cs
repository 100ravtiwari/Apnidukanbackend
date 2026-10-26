using Microsoft.EntityFrameworkCore;
using Dukan.API.Data;
using Dukan.API.DTOs.Cart;
using Dukan.API.DTOs.Common;
using Dukan.API.Models;

namespace Dukan.API.Services;

public class CartService : ICartService
{
    private readonly AppDbContext _context;
    private const decimal FreeDeliveryThreshold = 500.00m;
    private const decimal StandardDeliveryFee = 40.00m;

    public CartService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<CartSummaryDto>> GetCartAsync(int userId)
    {
        var items = await _context.CartItems
            .Include(c => c.Product)
            .Where(c => c.UserId == userId)
            .ToListAsync();

        return ApiResponse<CartSummaryDto>.Ok(BuildCartSummary(items));
    }

    public async Task<ApiResponse<CartSummaryDto>> AddToCartAsync(int userId, AddToCartDto dto)
    {
        var product = await _context.Products.FindAsync(dto.ProductId);
        if (product == null || !product.IsAvailable)
        {
            return ApiResponse<CartSummaryDto>.Fail("Product is not available.");
        }

        var cartItem = await _context.CartItems
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == dto.ProductId);

        var requestedTotalQuantity = (cartItem?.Quantity ?? 0) + dto.Quantity;
        if (product.StockQuantity < requestedTotalQuantity)
        {
            return ApiResponse<CartSummaryDto>.Fail($"Only {product.StockQuantity} units available in stock.");
        }

        if (cartItem == null)
        {
            cartItem = new CartItem
            {
                UserId = userId,
                ProductId = dto.ProductId,
                Quantity = dto.Quantity,
                AddedAt = DateTime.UtcNow
            };
            _context.CartItems.Add(cartItem);
        }
        else
        {
            cartItem.Quantity = requestedTotalQuantity;
        }

        await _context.SaveChangesAsync();
        return await GetCartAsync(userId);
    }

    public async Task<ApiResponse<CartSummaryDto>> UpdateQuantityAsync(int userId, int productId, int quantity)
    {
        var cartItem = await _context.CartItems
            .Include(c => c.Product)
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);

        if (cartItem == null)
        {
            return ApiResponse<CartSummaryDto>.Fail("Item not found in cart.");
        }

        if (quantity <= 0)
        {
            _context.CartItems.Remove(cartItem);
        }
        else
        {
            if (cartItem.Product != null && cartItem.Product.StockQuantity < quantity)
            {
                return ApiResponse<CartSummaryDto>.Fail($"Only {cartItem.Product.StockQuantity} units available in stock.");
            }
            cartItem.Quantity = quantity;
        }

        await _context.SaveChangesAsync();
        return await GetCartAsync(userId);
    }

    public async Task<ApiResponse<CartSummaryDto>> RemoveFromCartAsync(int userId, int productId)
    {
        var cartItem = await _context.CartItems
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);

        if (cartItem != null)
        {
            _context.CartItems.Remove(cartItem);
            await _context.SaveChangesAsync();
        }

        return await GetCartAsync(userId);
    }

    public async Task<ApiResponse<bool>> ClearCartAsync(int userId)
    {
        var items = await _context.CartItems.Where(c => c.UserId == userId).ToListAsync();
        _context.CartItems.RemoveRange(items);
        await _context.SaveChangesAsync();

        return ApiResponse<bool>.Ok(true, "Cart cleared successfully.");
    }

    private static CartSummaryDto BuildCartSummary(List<CartItem> items)
    {
        var itemDtos = items.Select(ci =>
        {
            var unitPrice = ci.Product?.DiscountPrice ?? ci.Product?.Price ?? 0m;
            return new CartItemDto
            {
                Id = ci.Id,
                ProductId = ci.ProductId,
                ProductName = ci.Product?.Name ?? "Unknown Product",
                ProductImage = ci.Product?.ImageUrl,
                Unit = ci.Product?.Unit ?? "piece",
                UnitPrice = unitPrice,
                Quantity = ci.Quantity,
                TotalPrice = unitPrice * ci.Quantity,
                AvailableStock = ci.Product?.StockQuantity ?? 0
            };
        }).ToList();

        var subTotal = itemDtos.Sum(i => i.TotalPrice);
        var deliveryFee = subTotal > 0 && subTotal < FreeDeliveryThreshold ? StandardDeliveryFee : 0m;

        return new CartSummaryDto
        {
            Items = itemDtos,
            TotalItems = itemDtos.Sum(i => i.Quantity),
            SubTotal = subTotal,
            DeliveryFee = deliveryFee,
            GrandTotal = subTotal + deliveryFee
        };
    }
}
