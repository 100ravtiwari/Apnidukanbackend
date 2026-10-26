using Microsoft.EntityFrameworkCore;
using Dukan.API.Data;
using Dukan.API.DTOs.Common;
using Dukan.API.DTOs.Orders;
using Dukan.API.Models;

namespace Dukan.API.Services;

public class OrderService : IOrderService
{
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private const decimal FreeDeliveryThreshold = 500.00m;
    private const decimal StandardDeliveryFee = 40.00m;

    public OrderService(AppDbContext context, IEmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    public async Task<ApiResponse<OrderDto>> CreateOrderAsync(int userId, CreateOrderDto dto)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            var cartItems = await _context.CartItems
                .Include(c => c.Product)
                .Where(c => c.UserId == userId)
                .ToListAsync();

            if (!cartItems.Any())
            {
                return ApiResponse<OrderDto>.Fail("Your cart is empty. Please add items before placing an order.");
            }

            // Validate stock
            foreach (var item in cartItems)
            {
                if (item.Product == null || !item.Product.IsAvailable)
                {
                    return ApiResponse<OrderDto>.Fail($"Product '{item.Product?.Name ?? "Item"}' is no longer available.");
                }

                if (item.Product.StockQuantity < item.Quantity)
                {
                    return ApiResponse<OrderDto>.Fail(
                        $"Insufficient stock for '{item.Product.Name}'. Only {item.Product.StockQuantity} available.");
                }
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                decimal subTotal = 0m;
                var orderItems = new List<OrderItem>();

                foreach (var item in cartItems)
                {
                    var unitPrice = item.Product!.DiscountPrice ?? item.Product.Price;
                    var totalItemPrice = unitPrice * item.Quantity;
                    subTotal += totalItemPrice;

                    // Deduct stock
                    item.Product.StockQuantity -= item.Quantity;
                    if (item.Product.StockQuantity <= 0)
                    {
                        item.Product.StockQuantity = 0;
                        item.Product.IsAvailable = false;
                    }

                    orderItems.Add(new OrderItem
                    {
                        ProductId = item.ProductId,
                        ProductName = item.Product.Name,
                        UnitPrice = unitPrice,
                        Quantity = item.Quantity,
                        TotalPrice = totalItemPrice
                    });
                }

                var deliveryFee = subTotal > 0 && subTotal < FreeDeliveryThreshold ? StandardDeliveryFee : 0m;
                var totalAmount = subTotal + deliveryFee;

                var randomNum = Random.Shared.Next(1000, 9999);
                var orderNumber = $"DKN-{DateTime.UtcNow:yyyyMMdd}-{randomNum}";

                var order = new Order
                {
                    OrderNumber = orderNumber,
                    UserId = userId,
                    OrderDate = DateTime.UtcNow,
                    SubTotal = subTotal,
                    DeliveryFee = deliveryFee,
                    TotalAmount = totalAmount,
                    Status = "Pending",
                    PaymentMethod = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? "COD" : dto.PaymentMethod.Trim().ToUpper(),
                    PaymentStatus = "Pending",
                    CustomerName = dto.CustomerName.Trim(),
                    CustomerPhone = dto.CustomerPhone.Trim(),
                    DeliveryAddress = dto.DeliveryAddress.Trim(),
                    DeliveryCity = dto.DeliveryCity.Trim(),
                    DeliveryPincode = dto.DeliveryPincode.Trim(),
                    Notes = dto.Notes?.Trim(),
                    OrderItems = orderItems
                };

                _context.Orders.Add(order);

                // Empty the cart
                _context.CartItems.RemoveRange(cartItems);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return ApiResponse<OrderDto>.Ok(MapToOrderDto(order), "Order placed successfully!");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiResponse<OrderDto>.Fail($"Failed to place order: {ex.Message}");
            }
        });
    }

    public async Task<ApiResponse<List<OrderDto>>> GetUserOrdersAsync(int userId)
    {
        var orders = await _context.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.User)
            .Include(o => o.AssignedRider)
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.OrderDate)
            .AsNoTracking()
            .ToListAsync();

        return ApiResponse<List<OrderDto>>.Ok(orders.Select(o => MapToOrderDto(o, maskOtp: false)).ToList());
    }

    public async Task<ApiResponse<OrderDto>> GetOrderByIdAsync(int orderId, int userId, bool isAdmin)
    {
        var query = _context.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.User)
            .Include(o => o.AssignedRider)
            .AsNoTracking()
            .AsQueryable();

        if (!isAdmin)
        {
            query = query.Where(o => o.UserId == userId);
        }

        var order = await query.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null)
        {
            return ApiResponse<OrderDto>.Fail("Order not found or access denied.");
        }

        return ApiResponse<OrderDto>.Ok(MapToOrderDto(order, maskOtp: !isAdmin && order.UserId != userId));
    }

    public async Task<ApiResponse<PagedResult<OrderDto>>> GetAllOrdersAsync(string? status, int pageNumber = 1, int pageSize = 20, bool maskOtp = false)
    {
        var query = _context.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.User)
            .Include(o => o.AssignedRider)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(o => o.Status.ToLower() == status.Trim().ToLower());
        }

        var totalCount = await query.CountAsync();
        pageNumber = pageNumber > 0 ? pageNumber : 1;
        pageSize = pageSize is > 0 and <= 100 ? pageSize : 20;

        var items = await query
            .OrderByDescending(o => o.OrderDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = new PagedResult<OrderDto>
        {
            Items = items.Select(o => MapToOrderDto(o, maskOtp)).ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        return ApiResponse<PagedResult<OrderDto>>.Ok(result);
    }

    public async Task<ApiResponse<OrderDto>> UpdateOrderStatusAsync(int orderId, string newStatus)
    {
        var validStatuses = new[] { "Pending", "Confirmed", "Packed", "Dispatched", "OutForDelivery", "Delivered", "Cancelled" };
        var normalizedStatus = validStatuses.FirstOrDefault(s => s.Equals(newStatus.Trim(), StringComparison.OrdinalIgnoreCase));

        if (normalizedStatus == null)
        {
            return ApiResponse<OrderDto>.Fail($"Invalid status. Allowed values: {string.Join(", ", validStatuses)}");
        }

        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
        {
            return ApiResponse<OrderDto>.Fail("Order not found.");
        }

        // If cancelled from non-cancelled status, restore stock
        if (normalizedStatus == "Cancelled" && order.Status != "Cancelled")
        {
            foreach (var item in order.OrderItems)
            {
                var product = await _context.Products.FindAsync(item.ProductId);
                if (product != null)
                {
                    product.StockQuantity += item.Quantity;
                    product.IsAvailable = true;
                }
            }
        }

        order.Status = normalizedStatus;
        if (normalizedStatus == "Delivered" && order.PaymentMethod == "COD")
        {
            order.PaymentStatus = "Completed";
        }

        await _context.SaveChangesAsync();

        return ApiResponse<OrderDto>.Ok(MapToOrderDto(order), $"Order status updated to {normalizedStatus}.");
    }

    public async Task<ApiResponse<OrderDto>> UpdatePaymentStatusAsync(int orderId, string paymentStatus)
    {
        var validStatuses = new[] { "Pending", "Completed", "Failed" };
        var normalizedStatus = validStatuses.FirstOrDefault(s => s.Equals(paymentStatus.Trim(), StringComparison.OrdinalIgnoreCase));

        if (normalizedStatus == null)
        {
            return ApiResponse<OrderDto>.Fail($"Invalid payment status. Allowed values: {string.Join(", ", validStatuses)}");
        }

        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
        {
            return ApiResponse<OrderDto>.Fail("Order not found.");
        }

        order.PaymentStatus = normalizedStatus;
        await _context.SaveChangesAsync();

        return ApiResponse<OrderDto>.Ok(MapToOrderDto(order), $"Payment status updated to {normalizedStatus}.");
    }

    public async Task<ApiResponse<OrderDto>> DispatchOrderAsync(int orderId, string? notes)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
        {
            return ApiResponse<OrderDto>.Fail("Order not found.");
        }

        if (order.Status == "Delivered")
        {
            return ApiResponse<OrderDto>.Fail("Order is already delivered.");
        }

        if (order.Status == "Cancelled")
        {
            return ApiResponse<OrderDto>.Fail("Cannot dispatch a cancelled order.");
        }

        // Generate 4-digit numeric verification OTP (e.g. 7492)
        var otp = Random.Shared.Next(1000, 9999).ToString();
        order.DeliveryOtp = otp;
        // FCFS: Set to "Dispatched" — no rider assigned yet, all riders will see it
        order.Status = "Dispatched";
        order.DispatchedAt = DateTime.UtcNow;
        order.AssignedRiderId = null;
        order.AssignedRider = null;

        // Do NOT auto-assign rider. FCFS: riders will accept themselves.

        if (!string.IsNullOrWhiteSpace(notes))
        {
            order.Notes = string.IsNullOrWhiteSpace(order.Notes) ? notes.Trim() : $"{order.Notes} | {notes.Trim()}";
        }

        await _context.SaveChangesAsync();

        // Send Email notification with OTP to customer
        var customerEmail = order.User?.Email;
        if (!string.IsNullOrWhiteSpace(customerEmail))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _emailService.SendDispatchOtpEmailAsync(
                        customerEmail,
                        order.CustomerName,
                        order.OrderNumber,
                        otp,
                        $"{order.DeliveryAddress}, {order.DeliveryCity} - {order.DeliveryPincode}",
                        order.TotalAmount);
                }
                catch
                {
                    // Email logging already handled inside service
                }
            });
        }

        return ApiResponse<OrderDto>.Ok(
            MapToOrderDto(order),
            $"Order #{order.OrderNumber} dispatched! Delivery OTP ({otp}) sent to {customerEmail ?? "customer"}. Waiting for a delivery partner to accept.");
    }

    public async Task<ApiResponse<OrderDto>> PackOrderAsync(int orderId, string? packedBy)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.User)
            .Include(o => o.AssignedRider)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
            return ApiResponse<OrderDto>.Fail("Order not found.");

        if (order.Status == "Cancelled")
            return ApiResponse<OrderDto>.Fail("Cannot pack a cancelled order.");

        if (order.Status == "Delivered")
            return ApiResponse<OrderDto>.Fail("Order is already delivered.");

        order.Status = "Packed";
        order.PackedBy = string.IsNullOrWhiteSpace(packedBy) ? "Order Manager" : packedBy.Trim();
        order.PackedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return ApiResponse<OrderDto>.Ok(MapToOrderDto(order, maskOtp: true), $"Order #{order.OrderNumber} packed and ready for dispatch.");
    }

    public async Task<ApiResponse<OrderDto>> AssignAndDispatchOrderAsync(int orderId, int riderId, string? notes)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
            return ApiResponse<OrderDto>.Fail("Order not found.");

        if (order.Status == "Delivered")
            return ApiResponse<OrderDto>.Fail("Order is already delivered.");

        if (order.Status == "Cancelled")
            return ApiResponse<OrderDto>.Fail("Cannot dispatch a cancelled order.");

        var rider = await _context.Users.FirstOrDefaultAsync(u => u.Id == riderId && u.Role == "DeliveryBoy");
        if (rider == null)
            return ApiResponse<OrderDto>.Fail("Selected delivery partner not found.");

        // Generate 4-digit OTP
        var otp = Random.Shared.Next(1000, 9999).ToString();
        order.DeliveryOtp = otp;
        order.Status = "OutForDelivery";
        order.DispatchedAt = DateTime.UtcNow;
        order.AssignedRiderId = riderId;
        order.AssignedRider = rider;

        if (!string.IsNullOrWhiteSpace(notes))
        {
            order.Notes = string.IsNullOrWhiteSpace(order.Notes) ? notes.Trim() : $"{order.Notes} | {notes.Trim()}";
        }

        await _context.SaveChangesAsync();

        // Send Email notification with OTP to customer
        var customerEmail = order.User?.Email;
        if (!string.IsNullOrWhiteSpace(customerEmail))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _emailService.SendDispatchOtpEmailAsync(
                        customerEmail,
                        order.CustomerName,
                        order.OrderNumber,
                        otp,
                        $"{order.DeliveryAddress}, {order.DeliveryCity} - {order.DeliveryPincode}",
                        order.TotalAmount);
                }
                catch { }
            });
        }

        // Return order with maskOtp true so OrderManager cannot see the OTP!
        return ApiResponse<OrderDto>.Ok(
            MapToOrderDto(order, maskOtp: true),
            $"Order #{order.OrderNumber} assigned to {rider.Name} and dispatched! Delivery OTP sent to customer.");
    }

    public async Task<ApiResponse<List<OrderDto>>> GetRiderOrdersAsync(int riderId, string? status = null)
    {
        var query = _context.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.User)
            .Include(o => o.AssignedRider)
            .AsNoTracking()
            .AsQueryable();

        if (riderId > 0)
        {
            query = query.Where(o => o.AssignedRiderId == riderId);
        }
        else
        {
            query = query.Where(o => o.AssignedRiderId != null);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status.Equals("OutForDelivery", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(o => o.Status.ToLower() == "outfordelivery" || o.Status.ToLower() == "dispatched");
            }
            else
            {
                query = query.Where(o => o.Status.ToLower() == status.Trim().ToLower());
            }
        }

        var orders = await query
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        return ApiResponse<List<OrderDto>>.Ok(orders.Select(o => MapToOrderDto(o, maskOtp: true)).ToList());
    }

    // FCFS: Get all dispatched orders that have no rider assigned yet
    public async Task<ApiResponse<List<OrderDto>>> GetAvailableDeliveriesAsync()
    {
        var orders = await _context.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.User)
            .Where(o => o.Status == "Dispatched" && o.AssignedRiderId == null)
            .OrderBy(o => o.DispatchedAt)
            .AsNoTracking()
            .ToListAsync();

        return ApiResponse<List<OrderDto>>.Ok(orders.Select(o => MapToOrderDto(o, maskOtp: true)).ToList());
    }

    // FCFS: First rider to call this gets the order. Uses transaction for race-condition safety.
    public async Task<ApiResponse<OrderDto>> AcceptDeliveryAsync(int orderId, int riderId)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .Include(o => o.User)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<OrderDto>.Fail("Order not found.");
                }

                // FCFS check: only "Dispatched" status with no rider can be accepted
                if (!string.Equals(order.Status?.Trim(), "Dispatched", StringComparison.OrdinalIgnoreCase) || order.AssignedRiderId != null)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<OrderDto>.Fail("This order has already been accepted by another delivery partner.");
                }

                var rider = await _context.Users.FirstOrDefaultAsync(u => u.Id == riderId && 
                    (u.Role.ToLower() == "deliveryboy" || u.Role.ToLower() == "admin" || u.Role.ToLower() == "ordermanager"));
                if (rider == null)
                {
                    await transaction.RollbackAsync();
                    return ApiResponse<OrderDto>.Fail("Delivery partner not found.");
                }

                // Assign rider and move to OutForDelivery
                order.AssignedRiderId = riderId;
                order.AssignedRider = rider;
                order.Status = "OutForDelivery";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return ApiResponse<OrderDto>.Ok(
                    MapToOrderDto(order, maskOtp: true),
                    $"Order #{order.OrderNumber} accepted! Deliver to {order.CustomerName} at {order.DeliveryAddress}.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiResponse<OrderDto>.Fail($"Failed to accept order: {ex.Message}");
            }
        });
    }

    public async Task<ApiResponse<OrderDto>> VerifyDeliveryOtpAsync(int orderId, string otp)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.User)
            .Include(o => o.AssignedRider)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
        {
            return ApiResponse<OrderDto>.Fail("Order not found.");
        }

        if (order.Status == "Delivered")
        {
            return ApiResponse<OrderDto>.Ok(MapToOrderDto(order), "Order is already marked as delivered.");
        }

        if (order.Status != "OutForDelivery")
        {
            return ApiResponse<OrderDto>.Fail($"Order is currently in '{order.Status}' status. Only dispatched orders can be verified and delivered.");
        }

        var cleanInputOtp = otp.Trim();
        var cleanStoredOtp = order.DeliveryOtp?.Trim();

        if (string.IsNullOrEmpty(cleanStoredOtp) || cleanStoredOtp != cleanInputOtp)
        {
            return ApiResponse<OrderDto>.Fail("Invalid OTP! Please enter the correct code received by the customer.");
        }

        // OTP Verified Successfully!
        order.Status = "Delivered";
        order.DeliveredAt = DateTime.UtcNow;

        // Auto-complete COD payment
        if (order.PaymentMethod == "COD")
        {
            order.PaymentStatus = "Completed";
        }

        await _context.SaveChangesAsync();

        return ApiResponse<OrderDto>.Ok(
            MapToOrderDto(order, maskOtp: true),
            $"OTP verified successfully! Order #{order.OrderNumber} is marked as Delivered.");
    }

    private static OrderDto MapToOrderDto(Order order, bool maskOtp = false)
    {
        return new OrderDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            UserId = order.UserId,
            OrderDate = order.OrderDate,
            SubTotal = order.SubTotal,
            DeliveryFee = order.DeliveryFee,
            TotalAmount = order.TotalAmount,
            Status = order.Status,
            PaymentMethod = order.PaymentMethod,
            PaymentStatus = order.PaymentStatus,
            CustomerName = order.CustomerName,
            CustomerPhone = order.CustomerPhone,
            CustomerEmail = order.User != null ? order.User.Email : null,
            DeliveryAddress = order.DeliveryAddress,
            DeliveryCity = order.DeliveryCity,
            DeliveryPincode = order.DeliveryPincode,
            Notes = order.Notes,
            DeliveryOtp = maskOtp ? null : order.DeliveryOtp,
            DispatchedAt = order.DispatchedAt,
            DeliveredAt = order.DeliveredAt,
            AssignedRiderId = order.AssignedRiderId,
            AssignedRiderName = order.AssignedRider != null ? order.AssignedRider.Name : null,
            AssignedRiderPhone = order.AssignedRider != null ? order.AssignedRider.Phone : null,
            PackedBy = order.PackedBy,
            PackedAt = order.PackedAt,
            Items = order.OrderItems.Select(oi => new OrderItemDto
            {
                Id = oi.Id,
                ProductId = oi.ProductId,
                ProductName = oi.ProductName,
                UnitPrice = oi.UnitPrice,
                Quantity = oi.Quantity,
                TotalPrice = oi.TotalPrice
            }).ToList()
        };
    }
}
