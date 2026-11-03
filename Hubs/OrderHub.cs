using Microsoft.AspNetCore.SignalR;

namespace Dukan.API.Hubs;

public class OrderHub : Hub
{
    // Clients can join specific groups, e.g. "Admins", "OrderManagers", "DeliveryBoys", or "User_<userId>"
    public async Task JoinRoleGroup(string role)
    {
        if (!string.IsNullOrEmpty(role))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, role);
        }
    }

    public async Task JoinUserGroup(string userId)
    {
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"User_{userId}");
        }
    }

    public async Task LeaveRoleGroup(string role)
    {
        if (!string.IsNullOrEmpty(role))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, role);
        }
    }
}

