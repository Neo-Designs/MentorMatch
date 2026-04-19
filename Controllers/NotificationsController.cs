using MentorMatch.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MentorMatch.Models;

namespace MentorMatch.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class NotificationsController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var notifications = await context.Notifications
            .Where(n => n.UserId == user.Id && !n.IsRead)
            .OrderByDescending(n => n.Timestamp)
            .Take(10)
            .ToListAsync();

        return Ok(notifications);
    }

    [HttpPost("mark-read")]
    public async Task<IActionResult> MarkAsRead()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var notifications = await context.Notifications
            .Where(n => n.UserId == user.Id && !n.IsRead)
            .ToListAsync();

        foreach (var n in notifications) n.IsRead = true;
        await context.SaveChangesAsync();

        return Ok();
    }
}
