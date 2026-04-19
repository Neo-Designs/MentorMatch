using MentorMatch.Data;
using MentorMatch.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MentorMatch.Controllers;

[Authorize(Roles = "Student")]
public class StudentController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager) : Controller
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
                Message = $"Your proposal for module {proposal.ModuleId} has been uploaded!" 
            });
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
        if (proposal.Status != ProposalStatus.Pending)
        {
            TempData["Error"] = "Only pending proposals can be edited.";
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
        if (existing.Status != ProposalStatus.Pending) return BadRequest();

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
