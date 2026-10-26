using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Dukan.API.DTOs.Cart;
using Dukan.API.DTOs.Common;
using Dukan.API.Services;

namespace Dukan.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CartController : ControllerBase
{
    private readonly ICartService _cartService;

    public CartController(ICartService cartService)
    {
        _cartService = cartService;
    }

    private int? CurrentUserId
    {
        get
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(idClaim, out var id) ? id : null;
        }
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<CartSummaryDto>>> GetCart()
    {
        if (CurrentUserId == null)
            return Unauthorized(ApiResponse<CartSummaryDto>.Fail("Unauthorized"));

        var response = await _cartService.GetCartAsync(CurrentUserId.Value);
        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<CartSummaryDto>>> AddToCart([FromBody] AddToCartDto dto)
    {
        if (CurrentUserId == null)
            return Unauthorized(ApiResponse<CartSummaryDto>.Fail("Unauthorized"));

        var response = await _cartService.AddToCartAsync(CurrentUserId.Value, dto);
        if (!response.Success)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }

    [HttpPut("{productId:int}")]
    public async Task<ActionResult<ApiResponse<CartSummaryDto>>> UpdateQuantity(int productId, [FromBody] UpdateCartItemDto dto)
    {
        if (CurrentUserId == null)
            return Unauthorized(ApiResponse<CartSummaryDto>.Fail("Unauthorized"));

        var response = await _cartService.UpdateQuantityAsync(CurrentUserId.Value, productId, dto.Quantity);
        if (!response.Success)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }

    [HttpDelete("{productId:int}")]
    public async Task<ActionResult<ApiResponse<CartSummaryDto>>> RemoveItem(int productId)
    {
        if (CurrentUserId == null)
            return Unauthorized(ApiResponse<CartSummaryDto>.Fail("Unauthorized"));

        var response = await _cartService.RemoveFromCartAsync(CurrentUserId.Value, productId);
        return Ok(response);
    }

    [HttpDelete("clear")]
    public async Task<ActionResult<ApiResponse<bool>>> ClearCart()
    {
        if (CurrentUserId == null)
            return Unauthorized(ApiResponse<bool>.Fail("Unauthorized"));

        var response = await _cartService.ClearCartAsync(CurrentUserId.Value);
        return Ok(response);
    }
}
