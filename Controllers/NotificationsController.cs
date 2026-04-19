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
        var userId = userManager.GetUserId(User);
        if (userId == null) return Unauthorized();

        var notifications = await context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.Timestamp)
            .Take(15)
            .Select(n => new
            {
                n.Id,
                n.Title,
                n.Message,
                n.LinkUrl,
                n.Timestamp,
                n.IsRead
            })
            .ToListAsync();

        return Ok(notifications);
    }

    [HttpPost("mark-read")]
    public async Task<IActionResult> MarkAsRead()
    {
        var userId = userManager.GetUserId(User);
        if (userId == null) return Unauthorized();

        var notifications = await context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var n in notifications) n.IsRead = true;
        await context.SaveChangesAsync();

        return Ok();
    }
}