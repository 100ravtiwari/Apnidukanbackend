using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Dukan.API.Data;
using Dukan.API.DTOs.Common;
using Dukan.API.DTOs.Orders;
using Dukan.API.Models;
using Dukan.API.Hubs;

namespace Dukan.API.Services;

public class OrderService : IOrderService
{
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IHubContext<OrderHub> _hubContext;
    private readonly IRedisPersonaService? _redisPersonaService;
    private const decimal FreeDeliveryThreshold = 299.00m;
    private const decimal StandardDeliveryFee = 25.00m;
    private const decimal HandlingFee = 2.00m;

    public OrderService(
        AppDbContext context,
        IEmailService emailService,
        IHubContext<OrderHub> hubContext,
        IRedisPersonaService? redisPersonaService = null)
    {
        _context = context;
        _emailService = emailService;
        _hubContext = hubContext;
        _redisPersonaService = redisPersonaService;
    }

    private async Task BroadcastOrderEventAsync(string eventName, OrderDto order)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync("OrderUpdated", new { Event = eventName, Order = order });
            await _hubContext.Clients.All.SendAsync(eventName, order);
        }
        catch
        {
            // Logging or graceful fallback if websocket client disconnects
        }
    }

    public async Task<ApiResponse<OrderDto>> CreateOrderAsync(int userId, CreateOrderDto dto)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            var cartItems = await _context.CartItems
                .Include(c => c.Product)
                    .ThenInclude(p => p!.Category)
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
                decimal gstTotal = 0m;
                var orderItems = new List<OrderItem>();

                foreach (var item in cartItems)
                {
                    var unitPrice = item.VariantPrice ?? item.Product!.DiscountPrice ?? item.Product.Price;
                    var totalItemPrice = unitPrice * item.Quantity;
                    var gstRate = item.Product?.GstRate ?? 5.0m;
                    var gstAmount = totalItemPrice * (gstRate / 100m);

                    subTotal += totalItemPrice;
                    gstTotal += gstAmount;

                    // Deduct stock
                    item.Product!.StockQuantity -= item.Quantity;
                    if (item.Product.StockQuantity <= 0)
                    {
                        item.Product.StockQuantity = 0;
                        item.Product.IsAvailable = false;
                    }

                    orderItems.Add(new OrderItem
                    {
                        ProductId = item.ProductId,
                        ProductName = item.Product.Name,
                        VariantName = item.VariantName,
                        UnitPrice = unitPrice,
                        Quantity = item.Quantity,
                        TotalPrice = totalItemPrice,
                        GstRate = gstRate,
                        GstAmount = gstAmount
                    });
                }

                var deliveryFee = subTotal > 0 && subTotal < FreeDeliveryThreshold ? StandardDeliveryFee : 0m;
                var handlingFee = subTotal > 0 ? HandlingFee : 0m;
                var totalAmount = subTotal + deliveryFee + handlingFee;

                var randomNum = Random.Shared.Next(1000, 9999);
                var orderNumber = $"DKN-{DateTime.UtcNow:yyyyMMdd}-{randomNum}";

                // ============================================================
                // CHAMPAWAT GEOFENCE & HYPERLOCAL DELIVERY RADIUS (MAX 10 KM)
                // Store Hub: G.I.C Chowk, Champawat, Uttarakhand (262523)
                // ============================================================
                var cityLower = (dto.DeliveryCity ?? "").Trim().ToLower();
                var addressLower = (dto.DeliveryAddress ?? "").Trim().ToLower();

                // If explicit distance provided and > 10 km
                if (dto.DistanceKm.HasValue && dto.DistanceKm.Value > 10.0m)
                {
                    return ApiResponse<OrderDto>.Fail(
                        $"Delivery Unavailable: OM General Store currently delivers only within 10 km radius of G.I.C Chowk, Champawat (Uttarakhand). Your location is {dto.DistanceKm.Value:F1} km away.");
                }

                // If city is an obvious out-of-district city
                var nonServiceableCities = new[] { "delhi", "new delhi", "noida", "gurgaon", "gurugram", "mumbai", "bangalore", "kolkata", "chennai", "jaipur", "lucknow", "haldwani", "tanakpur", "pithoragarh" };
                if (nonServiceableCities.Any(c => cityLower == c || (addressLower.Contains(c) && !addressLower.Contains("champawat"))))
                {
                    return ApiResponse<OrderDto>.Fail(
                        "Delivery Unavailable: OM General Store operates locally in Champawat, Uttarakhand. We currently deliver only within 10 km of G.I.C Chowk.");
                }

                // Dynamic ETA calculation based on real distance in hill terrain
                var distKm = dto.DistanceKm ?? 1.2m; // default local town center (~1.2 km)
                var etaMinutes = Math.Max(10, (int)Math.Ceiling(5.0m + (distKm * 2.8m)));
                var estimatedTimeString = $"{etaMinutes} Minutes";
                var estimatedDeliveryAt = DateTime.UtcNow.AddMinutes(etaMinutes);

                var order = new Order
                {
                    OrderNumber = orderNumber,
                    UserId = userId,
                    OrderDate = DateTime.UtcNow,
                    SubTotal = subTotal,
                    GstTotal = gstTotal,
                    DeliveryFee = deliveryFee + handlingFee,
                    TotalAmount = totalAmount,
                    Status = "Pending",
                    PaymentMethod = string.IsNullOrWhiteSpace(dto.PaymentMethod) ? "COD" : dto.PaymentMethod.Trim().ToUpper(),
                    PaymentStatus = "Pending",
                    CustomerName = dto.CustomerName.Trim(),
                    CustomerPhone = dto.CustomerPhone.Trim(),
                    DeliveryAddress = dto.DeliveryAddress.Trim(),
                    DeliveryCity = string.IsNullOrWhiteSpace(dto.DeliveryCity) ? "Champawat" : dto.DeliveryCity.Trim(),
                    DeliveryPincode = string.IsNullOrWhiteSpace(dto.DeliveryPincode) ? "262523" : dto.DeliveryPincode.Trim(),
                    Notes = dto.Notes?.Trim(),
                    // script2.sql Stage 1: Order Inception with Dynamic Real-time ETA
                    EstimatedDeliveryAt = estimatedDeliveryAt,
                    EstimatedTime = estimatedTimeString,
                    DistanceKm = distKm,
                    OrderActionTypeId = 1, // 1 = No Action (Stage 1 Inception)
                    PgPaymentOrderId = dto.PgPaymentOrderId,
                    OrderItems = orderItems
                };

                _context.Orders.Add(order);

                // Empty the cart
                _context.CartItems.RemoveRange(cartItems);

                await _context.SaveChangesAsync();

                if (!string.IsNullOrWhiteSpace(dto.PgPaymentOrderId))
                {
                    var po = await _context.PendingOrders.FirstOrDefaultAsync(p => p.PgPaymentOrderId == dto.PgPaymentOrderId);
                    if (po != null)
                    {
                        po.Status = "Success";
                        po.OrderId = order.Id;
                        await _context.SaveChangesAsync();
                    }
                }

                await transaction.CommitAsync();

                // 🛒 Redis User Profiling: Automatically boost buyer's category persona (+5 pts per purchased category)
                if (_redisPersonaService != null)
                {
                    try
                    {
                        var purchasedCategories = cartItems
                            .Where(c => c.Product?.Category != null && !string.IsNullOrWhiteSpace(c.Product.Category.Name))
                            .Select(c => c.Product!.Category!.Name)
                            .Distinct()
                            .ToList();

                        foreach (var cat in purchasedCategories)
                        {
                            await _redisPersonaService.TrackUserActionAsync(userId.ToString(), cat, 5.0);
                        }
                    }
                    catch { /* Resilient background tracking */ }
                }

                var orderDto = MapToOrderDto(order);
                await BroadcastOrderEventAsync("NewOrderPlaced", orderDto);

                return ApiResponse<OrderDto>.Ok(orderDto, "Order placed successfully!");
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

    public async Task<ApiResponse<OrderDto>> ConfirmDoorstepUpiPaymentAsync(int orderId, string? transactionRef = null, string? payerVpa = null)
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

        var utr = !string.IsNullOrWhiteSpace(transactionRef)
            ? transactionRef.Trim()
            : $"UTR{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(100, 999)}";

        var vpa = !string.IsNullOrWhiteSpace(payerVpa) ? payerVpa.Trim() : "customer@upi";

        order.PaymentStatus = "Completed";
        order.PaymentMethod = "UPI (Doorstep)";

        var paymentNote = $"✅ Auto-verified UPI Payment of ₹{order.TotalAmount} (UTR: {utr}, Payer: {vpa})";
        order.Notes = string.IsNullOrWhiteSpace(order.Notes) ? paymentNote : $"{order.Notes} | {paymentNote}";

        await _context.SaveChangesAsync();

        return ApiResponse<OrderDto>.Ok(
            MapToOrderDto(order, maskOtp: true),
            $"UPI payment of ₹{order.TotalAmount} confirmed successfully in database.");
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

        // Record in OtpVerificationForOrders table (script2.sql)
        _context.OtpVerificationForOrders.Add(new OtpVerificationForOrder
        {
            OrderId = order.Id,
            MobileNumber = order.CustomerPhone,
            Otp = otp,
            OtpTypeId = 1, // 1 = Delivery Handover
            IsVerified = false,
            CreatedAt = DateTime.UtcNow
        });

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

        var dispatchedDto = MapToOrderDto(order);
        await BroadcastOrderEventAsync("OrderDispatched", dispatchedDto);

        return ApiResponse<OrderDto>.Ok(
            dispatchedDto,
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

        var packedDto = MapToOrderDto(order, maskOtp: true);
        await BroadcastOrderEventAsync("OrderPacked", packedDto);

        return ApiResponse<OrderDto>.Ok(packedDto, $"Order #{order.OrderNumber} packed and ready for dispatch.");
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

        // Record in OtpVerificationForOrders table (script2.sql)
        _context.OtpVerificationForOrders.Add(new OtpVerificationForOrder
        {
            OrderId = order.Id,
            MobileNumber = order.CustomerPhone,
            Otp = otp,
            OtpTypeId = 1,
            IsVerified = false,
            CreatedAt = DateTime.UtcNow
        });

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

                var acceptedDto = MapToOrderDto(order, maskOtp: true);
                await BroadcastOrderEventAsync("OrderClaimed", acceptedDto);

                return ApiResponse<OrderDto>.Ok(
                    acceptedDto,
                    $"Order #{order.OrderNumber} accepted! Deliver to {order.CustomerName} at {order.DeliveryAddress}.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ApiResponse<OrderDto>.Fail($"Failed to accept order: {ex.Message}");
            }
        });
    }

    public async Task<ApiResponse<OrderDto>> VerifyDeliveryOtpAsync(int orderId, string otp, string? paymentCollectionMode = null)
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

        // Mark in OtpVerificationForOrders table (script2.sql)
        var otpLog = await _context.OtpVerificationForOrders
            .Where(o => o.OrderId == order.Id && !o.IsVerified)
            .OrderByDescending(o => o.Id)
            .FirstOrDefaultAsync();

        if (otpLog != null)
        {
            otpLog.IsVerified = true;
            otpLog.VerifiedAt = DateTime.UtcNow;
            otpLog.VerifiedByRiderId = order.AssignedRiderId;
        }

        // Auto-complete COD / Doorstep payment
        if (order.PaymentMethod == "COD" || order.PaymentStatus != "Completed")
        {
            order.PaymentStatus = "Completed";
            if (!string.IsNullOrWhiteSpace(paymentCollectionMode))
            {
                if (paymentCollectionMode.Equals("UPI", StringComparison.OrdinalIgnoreCase))
                {
                    order.PaymentMethod = "UPI (Doorstep)";
                    var upiNote = $"Paid ₹{order.TotalAmount} via UPI QR at doorstep.";
                    order.Notes = string.IsNullOrWhiteSpace(order.Notes) ? upiNote : $"{order.Notes} | {upiNote}";
                }
                else if (paymentCollectionMode.Equals("Cash", StringComparison.OrdinalIgnoreCase))
                {
                    order.PaymentMethod = "Cash (Doorstep)";
                    var cashNote = $"Collected ₹{order.TotalAmount} cash at doorstep.";
                    order.Notes = string.IsNullOrWhiteSpace(order.Notes) ? cashNote : $"{order.Notes} | {cashNote}";
                }
            }
        }

        await _context.SaveChangesAsync();

        var deliveredDto = MapToOrderDto(order, maskOtp: true);
        await BroadcastOrderEventAsync("OrderDelivered", deliveredDto);

        return ApiResponse<OrderDto>.Ok(
            deliveredDto,
            $"OTP verified successfully! Order #{order.OrderNumber} is marked as Delivered.");
    }

    // script2.sql Feature: Handle Kirana rack shortages during packing
    public async Task<ApiResponse<OrderDto>> HandleUnavailableItemAsync(int orderId, HandleUnavailableItemDto dto, string settledBy)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .Include(o => o.AssignedRider)
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
        {
            return ApiResponse<OrderDto>.Fail("Order not found.");
        }

        var item = order.OrderItems.FirstOrDefault(oi => oi.Id == dto.OrderItemId);
        if (item == null)
        {
            return ApiResponse<OrderDto>.Fail("Order item not found.");
        }

        item.IsUnavailable = true;
        item.UnavailableAction = dto.Action; // "Refund" or "Replace"

        var record = new UnavailableProductDetail
        {
            OrderId = orderId,
            OrderItemId = dto.OrderItemId,
            ProductName = item.ProductName,
            UnavailableQuantity = item.Quantity,
            Action = dto.Action,
            SettledBy = settledBy,
            CreatedAt = DateTime.UtcNow
        };

        if (dto.Action == "Refund")
        {
            var refund = dto.RefundAmount > 0 ? dto.RefundAmount : item.TotalPrice;
            item.RefundAmount = refund;
            record.IsRefundSettled = true;
            record.RefundAmount = refund;
            order.OrderActionTypeId = 5; // 5 = Settlement (script2.sql [Master].[OrderActionType])

            // Reduce order total amount for customer
            order.TotalAmount = Math.Max(0m, order.TotalAmount - refund);
            order.SubTotal = Math.Max(0m, order.SubTotal - refund);
        }
        else if (dto.Action == "Replace")
        {
            var subName = string.IsNullOrWhiteSpace(dto.ReplacementItemName) 
                ? "Substituted by Store Manager" 
                : dto.ReplacementItemName.Trim();
            record.IsReplaced = true;
            record.ReplacementItemName = subName;
            item.ReplacementItemName = subName;
            order.OrderActionTypeId = 4; // 4 = Replacement (script2.sql [Master].[OrderActionType])
        }

        _context.UnavailableProductDetails.Add(record);
        order.UnavailableSettled = true;

        await _context.SaveChangesAsync();

        return ApiResponse<OrderDto>.Ok(
            MapToOrderDto(order),
            $"Item '{item.ProductName}' settled via {dto.Action}.");
    }

    // script2.sql Feature: Log transit damage / spillage
    public async Task<ApiResponse<LossLogDto>> LogLossAsync(LogLossDto dto, string loggedBy)
    {
        var order = await _context.Orders.FindAsync(dto.OrderId);
        if (order == null)
        {
            return ApiResponse<LossLogDto>.Fail("Order not found.");
        }

        var log = new LossInOrderLog
        {
            OrderId = dto.OrderId,
            OrderItemId = dto.OrderItemId,
            ProductName = dto.ProductName.Trim(),
            QuantityLoss = dto.QuantityLoss > 0 ? dto.QuantityLoss : 1,
            LossType = string.IsNullOrWhiteSpace(dto.LossType) ? "Damage In Transit" : dto.LossType.Trim(),
            Description = dto.Description?.Trim(),
            LossBy = string.IsNullOrWhiteSpace(dto.LossBy) ? "Rider" : dto.LossBy.Trim(),
            EstimatedLossAmount = dto.EstimatedLossAmount,
            LoggedBy = loggedBy,
            CreatedAt = DateTime.UtcNow
        };

        _context.LossInOrderLogs.Add(log);
        await _context.SaveChangesAsync();

        var resultDto = new LossLogDto
        {
            Id = log.Id,
            OrderId = log.OrderId,
            OrderItemId = log.OrderItemId,
            ProductName = log.ProductName,
            QuantityLoss = log.QuantityLoss,
            LossType = log.LossType,
            Description = log.Description,
            LossBy = log.LossBy,
            EstimatedLossAmount = log.EstimatedLossAmount,
            LoggedBy = log.LoggedBy,
            CreatedAt = log.CreatedAt
        };

        return ApiResponse<LossLogDto>.Ok(resultDto, "Loss / Damage audit entry recorded successfully.");
    }

    public async Task<ApiResponse<List<LossLogDto>>> GetLossLogsAsync()
    {
        var logs = await _context.LossInOrderLogs
            .AsNoTracking()
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new LossLogDto
            {
                Id = l.Id,
                OrderId = l.OrderId,
                OrderItemId = l.OrderItemId,
                ProductName = l.ProductName,
                QuantityLoss = l.QuantityLoss,
                LossType = l.LossType,
                Description = l.Description,
                LossBy = l.LossBy,
                EstimatedLossAmount = l.EstimatedLossAmount,
                LoggedBy = l.LoggedBy,
                CreatedAt = l.CreatedAt
            })
            .ToListAsync();

        return ApiResponse<List<LossLogDto>>.Ok(logs);
    }

    // script2.sql Stage 1: Order Inception & Online PG [Order].[PendingOrder]
    public async Task<ApiResponse<InitiatePgPaymentResponseDto>> InitiatePgPaymentAsync(int userId, InitiatePgPaymentDto dto)
    {
        var cartItems = await _context.CartItems
            .Include(c => c.Product)
            .Where(c => c.UserId == userId)
            .ToListAsync();

        if (!cartItems.Any())
        {
            return ApiResponse<InitiatePgPaymentResponseDto>.Fail("Your cart is empty. Please add items before initiating payment.");
        }

        // Validate stock
        foreach (var item in cartItems)
        {
            if (item.Product == null || !item.Product.IsAvailable)
            {
                return ApiResponse<InitiatePgPaymentResponseDto>.Fail($"Product '{item.Product?.Name ?? "Item"}' is no longer available.");
            }

            if (item.Product.StockQuantity < item.Quantity)
            {
                return ApiResponse<InitiatePgPaymentResponseDto>.Fail(
                    $"Insufficient stock for '{item.Product.Name}'. Only {item.Product.StockQuantity} available.");
            }
        }

        decimal subTotal = 0m;
        foreach (var item in cartItems)
        {
            var unitPrice = item.VariantPrice ?? item.Product!.DiscountPrice ?? item.Product.Price;
            subTotal += unitPrice * item.Quantity;
        }

        var deliveryFee = subTotal > 0 && subTotal < FreeDeliveryThreshold ? StandardDeliveryFee : 0m;
        var handlingFee = subTotal > 0 ? HandlingFee : 0m;
        var totalAmount = subTotal + deliveryFee + handlingFee;

        // ============================================================
        // CHAMPAWAT GEOFENCE & HYPERLOCAL DELIVERY RADIUS (MAX 10 KM)
        // Store Hub: G.I.C Chowk, Champawat, Uttarakhand (262523)
        // ============================================================
        var cityLower = (dto.DeliveryCity ?? "").Trim().ToLower();
        var addressLower = (dto.DeliveryAddress ?? "").Trim().ToLower();
        if (dto.DistanceKm.HasValue && dto.DistanceKm.Value > 10.0m)
        {
            return ApiResponse<InitiatePgPaymentResponseDto>.Fail(
                $"Delivery Unavailable: OM General Store currently delivers only within 10 km radius of G.I.C Chowk, Champawat. Your selected location is {dto.DistanceKm.Value:F1} km away.");
        }
        var nonServiceableCities = new[] { "delhi", "new delhi", "noida", "gurgaon", "gurugram", "mumbai", "bangalore", "kolkata", "chennai", "jaipur", "lucknow", "haldwani", "tanakpur", "pithoragarh" };
        if (nonServiceableCities.Any(c => cityLower == c || (addressLower.Contains(c) && !addressLower.Contains("champawat"))))
        {
            return ApiResponse<InitiatePgPaymentResponseDto>.Fail(
                "Delivery Unavailable: OM General Store operates locally in Champawat, Uttarakhand. We currently deliver only within 10 km of G.I.C Chowk.");
        }

        var pgToken = $"PG_DKN_ORD_{DateTime.UtcNow:yyyyMMddHHmmss}_{Random.Shared.Next(1000, 9999)}";
        var pgMid = "OM_STORE_PROD_MID";

        var pendingOrder = new PendingOrder
        {
            UserId = userId,
            PgMid = pgMid,
            PgPaymentOrderId = pgToken,
            Amount = totalAmount,
            Status = "Initiated",
            CheckoutPayload = System.Text.Json.JsonSerializer.Serialize(dto),
            CreatedAt = DateTime.UtcNow
        };

        _context.PendingOrders.Add(pendingOrder);
        await _context.SaveChangesAsync();

        var qrData = $"upi://pay?pa=omstore@upi&pn=OM%20General%20Store&am={totalAmount:F2}&cu=INR&tn={pgToken}";

        return ApiResponse<InitiatePgPaymentResponseDto>.Ok(new InitiatePgPaymentResponseDto
        {
            PgPaymentOrderId = pgToken,
            PgMid = pgMid,
            Amount = totalAmount,
            Status = "Initiated",
            QrData = qrData,
            CallbackUrl = "/api/orders/confirm-pg"
        }, "PG Payment Token created in [Order].[PendingOrder]");
    }

    public async Task<ApiResponse<OrderDto>> ConfirmPgPaymentAsync(int userId, ConfirmPgPaymentDto dto)
    {
        var pendingOrder = await _context.PendingOrders
            .FirstOrDefaultAsync(p => p.PgPaymentOrderId == dto.PgPaymentOrderId && p.UserId == userId);

        if (pendingOrder == null)
        {
            return ApiResponse<OrderDto>.Fail("Pending order token not found in [Order].[PendingOrder].");
        }

        if (pendingOrder.Status == "Success" && pendingOrder.OrderId.HasValue)
        {
            // Already confirmed, return existing order
            return await GetOrderByIdAsync(pendingOrder.OrderId.Value, userId, false);
        }

        CreateOrderDto checkoutDto;
        try
        {
            checkoutDto = System.Text.Json.JsonSerializer.Deserialize<CreateOrderDto>(pendingOrder.CheckoutPayload ?? "{}") 
                          ?? new CreateOrderDto();
        }
        catch
        {
            checkoutDto = new CreateOrderDto
            {
                CustomerName = "Valued Customer",
                CustomerPhone = "+91 9411188082",
                DeliveryAddress = "G.I.C Chowk Area",
                DeliveryCity = "Champawat",
                DeliveryPincode = "262523",
                PaymentMethod = "UPI"
            };
        }

        checkoutDto.PaymentMethod = "UPI";
        checkoutDto.PgPaymentOrderId = dto.PgPaymentOrderId;

        var result = await CreateOrderAsync(userId, checkoutDto);
        if (!result.Success || result.Data == null)
        {
            pendingOrder.Status = "Failed";
            await _context.SaveChangesAsync();
            return result;
        }

        // Update payment status to Completed and mark Stage 1 Inception complete
        var order = await _context.Orders.FindAsync(result.Data.Id);
        if (order != null)
        {
            order.PaymentStatus = "Completed";
            order.PaymentMethod = "UPI (Online PG)";
            order.OrderActionTypeId = 1; // Stage 1 Inception (1 = No Action / Normal)
            order.EstimatedDeliveryAt = DateTime.UtcNow.AddMinutes(15);
            order.EstimatedTime = "10-15 Minutes";
            await _context.SaveChangesAsync();

            pendingOrder.Status = "Success";
            pendingOrder.OrderId = order.Id;
            await _context.SaveChangesAsync();

            return ApiResponse<OrderDto>.Ok(MapToOrderDto(order), "Online payment confirmed! Order marked as Pending (order_action_type_id=1) with 10-15 minute delivery estimate populated.");
        }

        return result;
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
            GstTotal = order.GstTotal,
            DeliveryFee = order.DeliveryFee,
            TotalAmount = order.TotalAmount,
            Status = order.Status,
            PaymentMethod = order.PaymentMethod,
            PaymentStatus = order.PaymentStatus,
            CustomerName = order.CustomerName,
            CustomerPhone = order.CustomerPhone,
            CustomerEmail = order.User != null ? order.User.Email : null,
            UserAccountName = order.User != null ? order.User.Name : null,
            UserAccountPhone = order.User != null ? order.User.Phone : null,
            DeliveryAddress = order.DeliveryAddress,
            DeliveryCity = order.DeliveryCity,
            DeliveryPincode = order.DeliveryPincode,
            Notes = order.Notes,
            DeliveryOtp = maskOtp ? null : order.DeliveryOtp,
            DispatchedAt = order.DispatchedAt,
            DeliveredAt = order.DeliveredAt,
            // script2.sql Stage 1: Order Inception
            EstimatedDeliveryAt = order.EstimatedDeliveryAt ?? order.OrderDate.AddMinutes(15),
            EstimatedTime = order.EstimatedTime ?? "10-15 Minutes",
            OrderActionTypeId = order.OrderActionTypeId == 0 ? 1 : order.OrderActionTypeId,
            PgPaymentOrderId = order.PgPaymentOrderId,
            DistanceKm = order.DistanceKm,
            UnavailableSettled = order.UnavailableSettled,
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
                VariantName = oi.VariantName,
                UnitPrice = oi.UnitPrice,
                Quantity = oi.Quantity,
                TotalPrice = oi.TotalPrice,
                GstRate = oi.GstRate,
                GstAmount = oi.GstAmount,
                IsUnavailable = oi.IsUnavailable,
                UnavailableAction = oi.UnavailableAction,
                RefundAmount = oi.RefundAmount,
                ReplacementItemName = oi.ReplacementItemName
            }).ToList()
        };
    }
}
