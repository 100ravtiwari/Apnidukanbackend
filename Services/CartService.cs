using Microsoft.EntityFrameworkCore;
using Dukan.API.Data;
using Dukan.API.DTOs.Cart;
using Dukan.API.DTOs.Common;
using Dukan.API.Models;

namespace Dukan.API.Services;

public class CartService : ICartService
{
    private readonly AppDbContext _context;
    private const decimal FreeDeliveryThreshold = 299.00m;
    private const decimal StandardDeliveryFee = 25.00m;
    private const decimal HandlingFee = 2.00m;

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

        // Match both ProductId and VariantName (so user can have 500g and 1kg in cart simultaneously)
        var cartItem = await _context.CartItems
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == dto.ProductId && c.VariantName == dto.VariantName);

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
                VariantName = dto.VariantName,
                VariantPrice = dto.VariantPrice,
                Quantity = dto.Quantity,
                AddedAt = DateTime.UtcNow
            };
            _context.CartItems.Add(cartItem);
        }
        else
        {
            cartItem.Quantity = requestedTotalQuantity;
            if (dto.VariantPrice.HasValue)
            {
                cartItem.VariantPrice = dto.VariantPrice.Value;
            }
        }

        await _context.SaveChangesAsync();
        return await GetCartAsync(userId);
    }

    public async Task<ApiResponse<CartSummaryDto>> UpdateQuantityAsync(int userId, int productId, int quantity, string? variantName = null)
    {
        var query = _context.CartItems
            .Include(c => c.Product)
            .Where(c => c.UserId == userId && c.ProductId == productId);

        if (!string.IsNullOrWhiteSpace(variantName))
        {
            query = query.Where(c => c.VariantName == variantName);
        }

        var cartItem = await query.FirstOrDefaultAsync();

        if (cartItem == null)
        {
            return ApiResponse<CartSummaryDto>.Fail("Item not found in cart.");
        }

        if (quantity <= 0)
        {
            _context.CartItems.Remove(cartItem);
            await _context.SaveChangesAsync();
            return await GetCartAsync(userId);
        }

        if (cartItem.Product != null && cartItem.Product.StockQuantity < quantity)
        {
            return ApiResponse<CartSummaryDto>.Fail($"Only {cartItem.Product.StockQuantity} units available.");
        }

        cartItem.Quantity = quantity;
        await _context.SaveChangesAsync();

        return await GetCartAsync(userId);
    }

    public async Task<ApiResponse<CartSummaryDto>> RemoveFromCartAsync(int userId, int productId, string? variantName = null)
    {
        var query = _context.CartItems
            .Where(c => c.UserId == userId && c.ProductId == productId);

        if (!string.IsNullOrWhiteSpace(variantName))
        {
            query = query.Where(c => c.VariantName == variantName);
        }

        var cartItem = await query.FirstOrDefaultAsync();

        if (cartItem == null)
        {
            return ApiResponse<CartSummaryDto>.Fail("Item not found in cart.");
        }

        _context.CartItems.Remove(cartItem);
        await _context.SaveChangesAsync();

        return await GetCartAsync(userId);
    }

    public async Task<ApiResponse<bool>> ClearCartAsync(int userId)
    {
        var items = await _context.CartItems
            .Where(c => c.UserId == userId)
            .ToListAsync();

        if (items.Any())
        {
            _context.CartItems.RemoveRange(items);
            await _context.SaveChangesAsync();
        }

        return ApiResponse<bool>.Ok(true, "Cart cleared successfully.");
    }

    private static CartSummaryDto BuildCartSummary(List<CartItem> items)
    {
        var itemDtos = items.Select(ci =>
        {
            var unitPrice = ci.VariantPrice ?? ci.Product?.DiscountPrice ?? ci.Product?.Price ?? 0m;
            var gstRate = ci.Product?.GstRate ?? 5.0m;
            var gstAmount = unitPrice * (gstRate / 100m) * ci.Quantity;

            return new CartItemDto
            {
                Id = ci.Id,
                ProductId = ci.ProductId,
                ProductName = ci.Product?.Name ?? "Unknown Product",
                VariantName = ci.VariantName,
                ProductImage = ci.Product?.ImageUrl,
                Unit = !string.IsNullOrWhiteSpace(ci.VariantName) ? ci.VariantName : (ci.Product?.Unit ?? "piece"),
                UnitPrice = unitPrice,
                GstRate = gstRate,
                GstAmount = gstAmount,
                Quantity = ci.Quantity,
                TotalPrice = unitPrice * ci.Quantity,
                AvailableStock = ci.Product?.StockQuantity ?? 0
            };
        }).ToList();

        var subTotal = itemDtos.Sum(i => i.TotalPrice);
        var totalGst = itemDtos.Sum(i => i.GstAmount);
        var deliveryFee = subTotal > 0 && subTotal < FreeDeliveryThreshold ? StandardDeliveryFee : 0m;
        var handlingFee = subTotal > 0 ? HandlingFee : 0m;

        return new CartSummaryDto
        {
            Items = itemDtos,
            TotalItems = itemDtos.Sum(i => i.Quantity),
            SubTotal = subTotal,
            TotalGst = totalGst,
            DeliveryFee = deliveryFee,
            HandlingFee = handlingFee,
            GrandTotal = subTotal + deliveryFee + handlingFee
        };
    }
}
