using MentorMatch.Data;
using MentorMatch.Models;
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
    public async Task<IActionResult> CreateAccount(string email, string password, string firstName, string lastName, UserType role)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName))
        {
            TempData["Error"] = "All fields (Email, Password, Name) are required.";
            return RedirectToAction(nameof(Whitelist));
        }

        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            TempData["Error"] = "User with this email already exists.";
            return RedirectToAction(nameof(Whitelist));
        }

        var newUser = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            UserType = role,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(newUser, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(newUser, role.ToString());

            // flagging invite as redeemed
            var whitelistEntry = await context.PredefinedEmails.FirstOrDefaultAsync(e => e.Email == email);
            if (whitelistEntry != null)
            {
                whitelistEntry.IsUsed = true;
                await context.SaveChangesAsync();
            }

            TempData["Success"] = $"Account for {email} created successfully.";
        }
        else
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
        }

        return RedirectToAction(nameof(Whitelist));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user == null) return NotFound();

        // Check if deleting self
        var currentUserId = userManager.GetUserId(User);
        if (id == currentUserId)
        {
            TempData["Error"] = "You cannot delete your own administrator account.";
            return RedirectToAction(nameof(Users));
        }

        // cleaning up student data
        if (user.UserType == UserType.Student)
        {
            var studentProposals = await context.Proposals
                .Include(p => p.Match)
                .Include(p => p.ProposalTags)
                .Where(p => p.StudentId == id)
                .ToListAsync();

            foreach (var prop in studentProposals)
            {
                if (prop.Match != null) context.Matches.Remove(prop.Match);
                context.ProposalTags.RemoveRange(prop.ProposalTags);
            }
            context.Proposals.RemoveRange(studentProposals);
        }

        // resetting supervisor state
        if (user.UserType == UserType.Supervisor)
        {
            // Expertise Tags
            var expertiseTags = await context.UserTags.Where(ut => ut.UserId == id).ToListAsync();
            context.UserTags.RemoveRange(expertiseTags);

            // putting projects back to pending
            var supervisorMatches = await context.Matches
                .Include(m => m.Proposal)
                .Where(m => m.SupervisorId == id)
                .ToListAsync();

            foreach (var match in supervisorMatches)
            {
                if (match.Proposal != null)
                {
                    match.Proposal.Status = ProposalStatus.Pending;
                }
                context.Matches.Remove(match);
            }
        }

        // removing alerts

        var notifications = await context.Notifications.Where(n => n.UserId == id).ToListAsync();
        context.Notifications.RemoveRange(notifications);

        // final account deletion
        var result = await userManager.DeleteAsync(user);
        if (result.Succeeded)
        {
            TempData["Success"] = $"User {user.Email} and all related data deleted.";
        }
        else
        {
            TempData["Error"] = "Failed to delete user account.";
        }

        await context.SaveChangesAsync();
        return RedirectToAction(nameof(Users));
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

    public async Task<IActionResult> Allocations()
    {
        var proposals = await context.Proposals
            .Include(p => p.Student)
            .Include(p => p.Module)
            .Include(p => p.Match)
                .ThenInclude(m => m.Supervisor)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        ViewBag.Supervisors = await context.Users
            .Where(u => u.UserType == UserType.Supervisor)
            .OrderBy(u => u.FirstName)
            .ToListAsync();

        return View(proposals);
    }

    public async Task<IActionResult> Analytics()
    {
        var matchesCount = await context.Matches.CountAsync();
        var studentsCount = await userManager.GetUsersInRoleAsync("Student");
        var supervisorsCount = await userManager.GetUsersInRoleAsync("Supervisor");

        ViewBag.Matches = matchesCount;
        ViewBag.Students = studentsCount.Count;
        ViewBag.Supervisors = supervisorsCount.Count;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMatch(int id)
    {
        var match = await context.Matches
            .Include(m => m.Proposal)
            .FirstOrDefaultAsync(m => m.ProposalId == id);

        if (match != null)
        {
            var proposal = match.Proposal;
            proposal.Status = ProposalStatus.Pending;

            // alerting student
            context.Notifications.Add(new Notification
            {
                UserId = proposal.StudentId,
                Title = "Admin Alert: Match Removed \u26A0\uFE0F", // Adding a warning emoji
                Message = $"Your match for '{proposal.Title}' has been unassigned by an administrator. Status returned to pending.",
                LinkUrl = $"/Student/Details/{proposal.Id}",
                Timestamp = DateTime.UtcNow,
                IsRead = false
            });

            // alerting supervisor
            context.Notifications.Add(new Notification
            {
                UserId = match.SupervisorId,
                Title = "Admin Alert: Match Removed \u26A0\uFE0F",
                Message = $"Your match for project '{proposal.Title}' has been removed by an administrator.",
                LinkUrl = $"/Supervisor/Details/{proposal.Id}",
                Timestamp = DateTime.UtcNow,
                IsRead = false
            });

            context.Matches.Remove(match);
            await context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Allocations));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReassignMatch(int proposalId, string supervisorId)
    {
        var proposal = await context.Proposals
            .Include(p => p.Match)
            .FirstOrDefaultAsync(p => p.Id == proposalId);

        if (proposal == null) return NotFound();

        var newSupervisor = await context.Users.FirstOrDefaultAsync(u => u.Id == supervisorId);
        if (newSupervisor == null) return NotFound();

        string? oldSupervisorId = proposal.Match?.SupervisorId;
        
        if (proposal.Match != null)
        {
            // Reassign existing match
            proposal.Match.SupervisorId = supervisorId;
            proposal.Match.MatchedAt = DateTime.UtcNow;
            
            // Notification for old supervisor
            if (!string.IsNullOrEmpty(oldSupervisorId) && oldSupervisorId != supervisorId)
            {
                context.Notifications.Add(new Notification 
                { 
                    UserId = oldSupervisorId, 
                    Message = $"ADMIN ALERT: Your match for project '{proposal.Title}' has been reassigned to another supervisor by an administrator." 
                });
            }
        }
        else
        {
            // Create new match for unassigned proposal
            proposal.Status = ProposalStatus.Matched;
            context.Matches.Add(new Match
            {
                ProposalId = proposalId,
                SupervisorId = supervisorId,
                MatchedAt = DateTime.UtcNow
            });
        }

        // Notification for student
        context.Notifications.Add(new Notification 
        { 
            UserId = proposal.StudentId, 
            Message = $"ADMIN ALERT: Your match for '{proposal.Title}' has been reassigned/assigned by an administrator." 
        });

        // Notification for new supervisor
        context.Notifications.Add(new Notification 
        { 
            UserId = supervisorId, 
            Message = $"ADMIN ALERT: You have been assigned to project '{proposal.Title}' by an administrator." 
        });

        await context.SaveChangesAsync();
        TempData["Success"] = "Match updated successfully.";
        return RedirectToAction(nameof(Allocations));
    }

    [HttpGet]
    public async Task<IActionResult> GetChartData()
    {
        var matchStats = await context.Proposals
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
            .ToListAsync();

        return Json(matchStats);
    }
}
