using Dukan.API.DTOs.Common;
using Dukan.API.DTOs.Dashboard;

namespace Dukan.API.Services;

public interface IDashboardService
{
    Task<ApiResponse<DashboardSummaryDto>> GetDashboardSummaryAsync();
}
