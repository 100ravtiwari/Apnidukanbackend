using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Dukan.API.DTOs.Common;
using Dukan.API.DTOs.Orders;
using Dukan.API.Services;

namespace Dukan.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    private int? CurrentUserId
    {
        get
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(idClaim, out var id) ? id : null;
        }
    }

    private bool IsAdmin => User.IsInRole("Admin");
    private bool IsOrderManager => User.IsInRole("OrderManager");
    private bool IsDeliveryBoy => User.IsInRole("DeliveryBoy");

    [HttpPost]
    public async Task<ActionResult<ApiResponse<OrderDto>>> CreateOrder([FromBody] CreateOrderDto dto)
    {
        if (CurrentUserId == null)
            return Unauthorized(ApiResponse<OrderDto>.Fail("Unauthorized"));

        var response = await _orderService.CreateOrderAsync(CurrentUserId.Value, dto);
        if (!response.Success)
        {
            return BadRequest(response);
        }
        return CreatedAtAction(nameof(GetOrderById), new { id = response.Data?.Id }, response);
    }

    [HttpGet("my-orders")]
    public async Task<ActionResult<ApiResponse<List<OrderDto>>>> GetMyOrders()
    {
        if (CurrentUserId == null)
            return Unauthorized(ApiResponse<List<OrderDto>>.Fail("Unauthorized"));

        var response = await _orderService.GetUserOrdersAsync(CurrentUserId.Value);
        return Ok(response);
    }

    [Authorize(Roles = "Admin,OrderManager,DeliveryBoy")]
    [HttpGet("my-deliveries")]
    public async Task<ActionResult<ApiResponse<List<OrderDto>>>> GetMyDeliveries(
        [FromQuery] string? status,
        [FromQuery] int? riderId)
    {
        if (CurrentUserId == null)
            return Unauthorized(ApiResponse<List<OrderDto>>.Fail("Unauthorized"));

        bool isFleetSupervisor = IsAdmin || User.IsInRole("OrderManager");
        int targetRiderId = CurrentUserId.Value;
        if (isFleetSupervisor)
        {
            targetRiderId = (riderId.HasValue && riderId.Value > 0) ? riderId.Value : 0;
        }

        var response = await _orderService.GetRiderOrdersAsync(targetRiderId, status);
        return Ok(response);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> GetOrderById(int id)
    {
        if (CurrentUserId == null)
            return Unauthorized(ApiResponse<OrderDto>.Fail("Unauthorized"));

        var response = await _orderService.GetOrderByIdAsync(id, CurrentUserId.Value, IsAdmin);
        if (!response.Success)
        {
            return NotFound(response);
        }
        return Ok(response);
    }

    [Authorize(Roles = "Admin,OrderManager,DeliveryBoy")]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<OrderDto>>>> GetAllOrders(
        [FromQuery] string? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        // Delivery boy should only get their assigned orders
        if (IsDeliveryBoy && CurrentUserId.HasValue)
        {
            var riderOrders = await _orderService.GetRiderOrdersAsync(CurrentUserId.Value, status);
            var paged = new PagedResult<OrderDto>
            {
                Items = riderOrders.Data ?? new List<OrderDto>(),
                TotalCount = riderOrders.Data?.Count ?? 0,
                PageNumber = 1,
                PageSize = riderOrders.Data?.Count ?? 20
            };
            return Ok(ApiResponse<PagedResult<OrderDto>>.Ok(paged));
        }

        // OrderManager does NOT see OTP (maskOtp = true); Admin sees OTP (maskOtp = false)
        var maskOtp = !IsAdmin;
        var response = await _orderService.GetAllOrdersAsync(status, pageNumber, pageSize, maskOtp);
        return Ok(response);
    }

    [Authorize(Roles = "Admin,OrderManager")]
    [HttpPost("{id:int}/pack")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> PackOrder(int id, [FromBody] PackOrderDto? dto)
    {
        var packedBy = !string.IsNullOrWhiteSpace(dto?.PackedBy) 
            ? dto.PackedBy 
            : (User.Identity?.Name ?? "Order Manager");

        var response = await _orderService.PackOrderAsync(id, packedBy);
        if (!response.Success)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }

    [Authorize(Roles = "Admin,OrderManager")]
    [HttpPost("{id:int}/assign-and-dispatch")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> AssignAndDispatch(int id, [FromBody] AssignAndDispatchDto dto)
    {
        var response = await _orderService.AssignAndDispatchOrderAsync(id, dto.RiderId, dto.Notes);
        if (!response.Success)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> UpdateStatus(int id, [FromBody] UpdateOrderStatusDto dto)
    {
        var response = await _orderService.UpdateOrderStatusAsync(id, dto.Status);
        if (!response.Success)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:int}/payment-status")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> UpdatePaymentStatus(int id, [FromBody] UpdatePaymentStatusDto dto)
    {
        var response = await _orderService.UpdatePaymentStatusAsync(id, dto.PaymentStatus);
        if (!response.Success)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }

    [Authorize(Roles = "Admin,OrderManager")]
    [HttpPost("{id:int}/dispatch")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> DispatchOrder(int id, [FromBody] DispatchOrderDto? dto)
    {
        var response = await _orderService.DispatchOrderAsync(id, dto?.Notes);
        if (!response.Success)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }

    [Authorize(Roles = "Admin,OrderManager,DeliveryBoy")]
    [HttpPost("{id:int}/verify-delivery")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> VerifyDelivery(int id, [FromBody] VerifyDeliveryOtpDto dto)
    {
        var response = await _orderService.VerifyDeliveryOtpAsync(id, dto.Otp);
        if (!response.Success)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }

    // FCFS: All delivery boys can see available (dispatched, unassigned) orders
    [Authorize(Roles = "Admin,OrderManager,DeliveryBoy")]
    [HttpGet("available-deliveries")]
    public async Task<ActionResult<ApiResponse<List<OrderDto>>>> GetAvailableDeliveries()
    {
        var response = await _orderService.GetAvailableDeliveriesAsync();
        return Ok(response);
    }

    // FCFS: Delivery boy (or Admin / Manager supervisor) to accept gets the order
    [Authorize(Roles = "Admin,DeliveryBoy,OrderManager")]
    [HttpPost("{id:int}/accept-delivery")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> AcceptDelivery(int id)
    {
        if (CurrentUserId == null)
            return Unauthorized(ApiResponse<OrderDto>.Fail("Unauthorized"));

        var response = await _orderService.AcceptDeliveryAsync(id, CurrentUserId.Value);
        if (!response.Success)
        {
            return BadRequest(response);
        }
        return Ok(response);
    }
}
