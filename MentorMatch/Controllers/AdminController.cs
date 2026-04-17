using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MentorMatch.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager) : Controller
{
    public async Task<IActionResult> Whitelist()
    {
        var emails = await context.PredefinedEmails
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
        return View(emails);
    }

    public async Task<IActionResult> Users()
    {
        var users = await userManager.Users
            .OrderBy(u => u.UserType)
            .ToListAsync();
        return View(users);
    }

    public async Task<IActionResult> Modules()
    {
        var modules = await context.Modules
            .Include(m => m.Proposals)
            .ToListAsync();
        return View(modules);
    }

    [HttpPost]
    public async Task<IActionResult> AddModule(string name, string code)
    {
        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(code))
        {
            context.Modules.Add(new Module { Name = name, Code = code });
            await context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Modules));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteModule(int id)
    {
        var module = await context.Modules.FindAsync(id);
        if (module != null)
        {
            context.Modules.Remove(module);
            await context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Modules));
    }

    public async Task<IActionResult> Tags()
    {
        var tags = await context.Tags.ToListAsync();
        return View(tags);
    }

    [HttpPost]
    public async Task<IActionResult> AddTag(string name)
    {
        if (!string.IsNullOrEmpty(name))
        {
            context.Tags.Add(new Tag { Name = name });
            await context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Tags));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteTag(int id)
    {
        var tag = await context.Tags.FindAsync(id);
        if (tag != null)
        {
            context.Tags.Remove(tag);
            await context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Tags));
    }

    [HttpPost]
    public async Task<IActionResult> IssueEmail(string email, string firstName, string lastName, UserType role)
    {
        var admin = await userManager.GetUserAsync(User);
        if (admin == null) return Unauthorized();

        if (await context.PredefinedEmails.AnyAsync(e => e.Email == email))
        {
            TempData["Error"] = "Email already whitelisted.";
            return RedirectToAction(nameof(Whitelist));
        }

        var entry = new PredefinedEmail
        {
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            RoleRequested = role,
            CreatedByAdminId = admin.Id,
            CreatedAt = DateTime.UtcNow
        };

        context.PredefinedEmails.Add(entry);
        await context.SaveChangesAsync();

        TempData["Success"] = $"Invite issued for {email}.";
        return RedirectToAction(nameof(Whitelist));
    }
}