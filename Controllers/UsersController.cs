using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Dukan.API.Data;
using Dukan.API.DTOs.Auth;
using Dukan.API.DTOs.Common;
using Dukan.API.Models;

namespace Dukan.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;

    public UsersController(AppDbContext context)
    {
        _context = context;
    }

    private int? CurrentUserId
    {
        get
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(idClaim, out var id) ? id : null;
        }
    }

    // GET: /api/users/delivery-partners (Admin & OrderManager can see available delivery partners)
    [Authorize(Roles = "Admin,OrderManager")]
    [HttpGet("delivery-partners")]
    public async Task<ActionResult<ApiResponse<List<DeliveryPartnerStatusDto>>>> GetDeliveryPartners()
    {
        var riders = await _context.Users
            .Where(u => u.Role == "DeliveryBoy")
            .AsNoTracking()
            .ToListAsync();

        // Count active deliveries for each rider (status == 'OutForDelivery')
        var activeDeliveryCounts = await _context.Orders
            .Where(o => o.AssignedRiderId != null && o.Status == "OutForDelivery")
            .GroupBy(o => o.AssignedRiderId!.Value)
            .Select(g => new { RiderId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RiderId, x => x.Count);

        var result = riders.Select(r => new DeliveryPartnerStatusDto
        {
            Id = r.Id,
            Name = r.Name,
            Email = r.Email,
            Phone = r.Phone,
            ActiveDeliveriesCount = activeDeliveryCounts.TryGetValue(r.Id, out var count) ? count : 0
        }).OrderBy(r => r.ActiveDeliveriesCount).ThenBy(r => r.Name).ToList();

        return Ok(ApiResponse<List<DeliveryPartnerStatusDto>>.Ok(result));
    }

    // GET: /api/users (Admin only: list staff members)
    [Authorize(Roles = "Admin")]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<UserDto>>>> GetAllStaffUsers([FromQuery] string? role)
    {
        var query = _context.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(u => u.Role.ToLower() == role.Trim().ToLower());
        }
        else
        {
            // Default to all staff roles
            query = query.Where(u => u.Role == "Admin" || u.Role == "OrderManager" || u.Role == "DeliveryBoy");
        }

        var users = await query.OrderBy(u => u.Role).ThenBy(u => u.Name).ToListAsync();
        var dtos = users.Select(u => new UserDto
        {
            Id = u.Id,
            Name = u.Name,
            Email = u.Email,
            Phone = u.Phone,
            Address = u.Address,
            City = u.City,
            Pincode = u.Pincode,
            Role = u.Role
        }).ToList();

        return Ok(ApiResponse<List<UserDto>>.Ok(dtos));
    }

    // POST: /api/users (Admin only: create new OrderManager or DeliveryBoy)
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<UserDto>>> CreateStaffUser([FromBody] CreateStaffUserDto dto)
    {
        var emailClean = dto.Email.Trim().ToLower();
        var existing = await _context.Users.AnyAsync(u => u.Email.ToLower() == emailClean);
        if (existing)
        {
            return BadRequest(ApiResponse<UserDto>.Fail("A user with this email address already exists."));
        }

        var allowedRoles = new[] { "Admin", "OrderManager", "DeliveryBoy" };
        var normalizedRole = allowedRoles.FirstOrDefault(r => r.Equals(dto.Role.Trim(), StringComparison.OrdinalIgnoreCase));
        if (normalizedRole == null)
        {
            return BadRequest(ApiResponse<UserDto>.Fail($"Invalid role '{dto.Role}'. Allowed: {string.Join(", ", allowedRoles)}"));
        }

        var user = new User
        {
            Name = dto.Name.Trim(),
            Email = emailClean,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Phone = dto.Phone?.Trim(),
            Address = dto.Address?.Trim(),
            City = dto.City?.Trim(),
            Pincode = dto.Pincode?.Trim(),
            Role = normalizedRole,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var result = new UserDto
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Phone = user.Phone,
            Address = user.Address,
            City = user.City,
            Pincode = user.Pincode,
            Role = user.Role
        };

        return Ok(ApiResponse<UserDto>.Ok(result, $"Staff member '{user.Name}' created successfully with role '{user.Role}'."));
    }

    // DELETE: /api/users/{id} (Admin only)
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteStaffUser(int id)
    {
        if (CurrentUserId == id)
        {
            return BadRequest(ApiResponse<bool>.Fail("You cannot delete your own admin account."));
        }

        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound(ApiResponse<bool>.Fail("User not found."));
        }

        // Check if delivery boy has active orders
        var hasActiveOrders = await _context.Orders.AnyAsync(o => o.AssignedRiderId == id && (o.Status == "OutForDelivery" || o.Status == "Packed"));
        if (hasActiveOrders)
        {
            return BadRequest(ApiResponse<bool>.Fail("Cannot delete this staff member because they are currently assigned to active deliveries. Reassign orders first."));
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return Ok(ApiResponse<bool>.Ok(true, $"User '{user.Name}' deleted successfully."));
    }
}
