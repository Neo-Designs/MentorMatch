using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MentorMatch.Data;
using MentorMatch.Models;
using Microsoft.AspNetCore.SignalR;
using MentorMatch.Hubs;

namespace MentorMatch.Controllers;

[Authorize(Roles = "Student")]
public class StudentController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    IHubContext<NotificationHub> hubContext) : Controller
{
    public async Task<IActionResult> Dashboard()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var proposals = await context.Proposals
            .Include(p => p.Module)
            .Include(p => p.ProposalTags)
                .ThenInclude(pt => pt.Tag)
            .Include(p => p.Match)
                .ThenInclude(m => m.Supervisor)
            .Where(p => p.StudentId == user.Id)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return View(proposals);
    }

    public async Task<IActionResult> Details(int id)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var proposal = await context.Proposals
            .Include(p => p.Module)
            .Include(p => p.ProposalTags)
                .ThenInclude(pt => pt.Tag)
            .Include(p => p.Match)
                .ThenInclude(m => m.Supervisor)
            .FirstOrDefaultAsync(p => p.Id == id && p.StudentId == user.Id);

        if (proposal == null) return NotFound();

        return View(proposal);
    }

    [HttpGet]
    public async Task<IActionResult> Submit()
    {
        ViewBag.Modules = await context.Modules.ToListAsync();
        ViewBag.Tags = await context.Tags.ToListAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(Proposal proposal, int[] selectedTags, IFormFile? proposalPdf)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        ModelState.Remove("StudentId");
        ModelState.Remove("Module");
        ModelState.Remove("Student");

        if (ModelState.IsValid)
        {
            proposal.StudentId = user.Id;
            proposal.CreatedAt = DateTime.UtcNow;
            proposal.Status = ProposalStatus.Pending;

            if (proposalPdf != null)
            {
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(proposalPdf.FileName);
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", fileName);
                
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await proposalPdf.CopyToAsync(stream);
                }
                proposal.FilePath = "/uploads/" + fileName;
            }

            context.Proposals.Add(proposal);
            await context.SaveChangesAsync();

            foreach (var tagId in selectedTags)
            {
                context.ProposalTags.Add(new ProposalTag { ProposalId = proposal.Id, TagId = tagId });
            }
            
            await context.SaveChangesAsync();

            // Create notification
            context.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Title = "Proposal Submitted",
                Message = $"Your proposal '{proposal.Title}' has been successfully uploaded!",
                Timestamp = DateTime.UtcNow,
                IsRead = false
            });

            var matchingSupervisors = await userManager.GetUsersInRoleAsync("Supervisor");
            var supervisorsToNotify = matchingSupervisors
                .Where(s => context.UserTags.Any(ut => ut.UserId == s.Id && selectedTags.Contains(ut.TagId)))
                .ToList();

            foreach (var supervisor in supervisorsToNotify)
            {
                context.Notifications.Add(new Notification
                {
                    UserId = supervisor.Id,
                    Title = "New Expertise Match \u2728",
                    Message = $"A new project '{proposal.Title}' matches your expertise tags.",
                    LinkUrl = $"/Supervisor/Details/{proposal.Id}",
                    Timestamp = DateTime.UtcNow,
                    IsRead = false
                });
            }

            await context.SaveChangesAsync();

            return RedirectToAction(nameof(Dashboard));
        }

        ViewBag.Modules = await context.Modules.ToListAsync();
        ViewBag.Tags = await context.Tags.ToListAsync();
        return View(proposal);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var proposal = await context.Proposals
            .FirstOrDefaultAsync(p => p.Id == id && p.StudentId == user.Id);

        if (proposal == null) return NotFound();
        if (proposal.Status == ProposalStatus.Matched)
        {
            TempData["Error"] = "Matched projects cannot be edited.";
            return RedirectToAction(nameof(Dashboard));
        }

        ViewBag.Modules = await context.Modules.ToListAsync();
        ViewBag.Tags = await context.Tags.ToListAsync();
        ViewBag.SelectedTags = await context.ProposalTags
            .Where(pt => pt.ProposalId == id)
            .Select(pt => pt.TagId)
            .ToListAsync();

        return View(proposal);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Proposal proposal, int[] selectedTags, IFormFile? proposalPdf)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var existing = await context.Proposals
            .Include(p => p.ProposalTags)
            .FirstOrDefaultAsync(p => p.Id == proposal.Id && p.StudentId == user.Id);

        if (existing == null) return NotFound();
        if (existing.Status == ProposalStatus.Matched) return BadRequest();
        
        string? reviewerId = existing.Match?.SupervisorId;
        bool isUnderReview = existing.Status == ProposalStatus.UnderReview;

        ModelState.Remove("StudentId");
        ModelState.Remove("Module");
        ModelState.Remove("Student");

        if (ModelState.IsValid)
        {
            existing.Title = proposal.Title;
            existing.Abstract = proposal.Abstract;
            existing.TechnicalStack = proposal.TechnicalStack;
            existing.ResearchArea = proposal.ResearchArea;
            existing.ModuleId = proposal.ModuleId;
            existing.ProjectType = proposal.ProjectType;

            if (proposalPdf != null)
            {
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(proposalPdf.FileName);
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await proposalPdf.CopyToAsync(stream);
                }
                existing.FilePath = "/uploads/" + fileName;
            }

            // Update Tags
            context.ProposalTags.RemoveRange(existing.ProposalTags);
            foreach (var tagId in selectedTags)
            {
                context.ProposalTags.Add(new ProposalTag { ProposalId = existing.Id, TagId = tagId });
            }

            await context.SaveChangesAsync();

            // Notify supervisor if it's under review or matched (though matched is blocked above, 
            // staying defensive if we allow it later)
            if (!string.IsNullOrEmpty(reviewerId))
            {
                var notification = new Notification
                {
                    UserId = reviewerId,
                    Title = "Project Updated",
                    Message = $"The student has updated project '{existing.Title}' which was under your review.",
                    LinkUrl = $"/Supervisor/Details/{existing.Id}",
                    Timestamp = DateTime.UtcNow,
                    IsRead = false
                };
                context.Notifications.Add(notification);
                await hubContext.Clients.User(reviewerId).SendAsync("ReceiveNotification", notification);
                await context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Dashboard));
        }

        ViewBag.Modules = await context.Modules.ToListAsync();
        ViewBag.Tags = await context.Tags.ToListAsync();
        return View(proposal);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var proposal = await context.Proposals
            .FirstOrDefaultAsync(p => p.Id == id && p.StudentId == user.Id);

        if (proposal == null) return NotFound();
        if (proposal.Status != ProposalStatus.Pending) return BadRequest();

        var admins = await userManager.GetUsersInRoleAsync("Admin");
        foreach (var admin in admins)
        {
            context.Notifications.Add(new Notification
            {
                UserId = admin.Id,
                Title = "Project Withdrawn",
                Message = $"A student has withdrawn the proposal: '{proposal.Title}'.",
                LinkUrl = "/Admin/Allocations",
                Timestamp = DateTime.UtcNow,
                IsRead = false
            });
        }
        await context.SaveChangesAsync();

        context.Proposals.Remove(proposal);
        await context.SaveChangesAsync();

        return RedirectToAction(nameof(Dashboard));
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();
        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ApplicationUser model)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        user.FirstName = model.FirstName;
        user.LastName = model.LastName;
        user.Degree = model.Degree;
        user.StudentNumber = model.StudentNumber;
        user.ContactDetails = model.ContactDetails;

        await userManager.UpdateAsync(user);
        return RedirectToAction(nameof(Dashboard));
    }
}