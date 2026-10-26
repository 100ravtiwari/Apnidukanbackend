using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Dukan.API.DTOs.Common;
using Dukan.API.DTOs.Dashboard;
using Dukan.API.Services;

namespace Dukan.API.Controllers;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<DashboardSummaryDto>>> GetSummary()
    {
        var response = await _dashboardService.GetDashboardSummaryAsync();
        return Ok(response);
    }
}
