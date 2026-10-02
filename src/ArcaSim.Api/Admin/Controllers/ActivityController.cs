using ArcaSim.Application;
using ArcaSim.Application.Events;
using ArcaSim.Application.Traffic;
using ArcaSim.Application.Wsfe;
using ArcaSim.Domain;
using ArcaSim.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace ArcaSim.Api.Admin;

/// <summary>The live log: tickets, CAEs, rejections and refusals, newest first.</summary>
[ApiController]
[Route(AdminRoutes.Prefix + "/activity")]
public sealed class ActivityController(ActivityLog activity) : ControllerBase
{
    [HttpGet]
    public IReadOnlyList<Activity> Latest(int? limit) => activity.Latest(limit ?? 50);
}
