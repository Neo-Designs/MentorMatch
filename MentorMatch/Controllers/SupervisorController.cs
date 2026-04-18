using MentorMatch.Data;
using MentorMatch.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MentorMatch.Controllers;

[Authorize(Roles = "Supervisor")]
public class SupervisorController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager) : Controller
{
    public async Task<IActionResult> Dashboard(int? moduleId, int? tagId, bool smartSort = false)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        // Get Supervisor Expertise Tags
        var myTags = await context.UserTags
            .Where(ut => ut.UserId == user.Id)
            .Select(ut => ut.TagId)
            .ToListAsync();

        var query = context.Proposals
            .Include(p => p.Module)
            .Include(p => p.ProposalTags)
                .ThenInclude(pt => pt.Tag)
            .Where(p => p.Status != ProposalStatus.Matched);

        if (moduleId.HasValue) query = query.Where(p => p.ModuleId == moduleId.Value);

        if (tagId.HasValue)
            query = query.Where(p => p.ProposalTags.Any(pt => pt.TagId == tagId.Value));

        var proposals = await query.ToListAsync();

        if (smartSort)
        {
            // Performance: Prefetch match counts to ensure sorting is robust
            proposals = proposals
                .Select(p => new { Proposal = p, MatchCount = p.ProposalTags.Count(pt => myTags.Contains(pt.TagId)) })
                .OrderByDescending(x => x.MatchCount)
                .ThenByDescending(x => x.Proposal.CreatedAt)
                .Select(x => x.Proposal)
                .ToList();
        }
        else
        {
            proposals = proposals.OrderByDescending(p => p.CreatedAt).ToList();
        }

        ViewBag.Modules = await context.Modules.ToListAsync();
        ViewBag.ExpertiseTags = await context.Tags
            .Where(t => myTags.Contains(t.Id))
            .ToListAsync();
        ViewBag.ActiveModule = moduleId;
        ViewBag.ActiveTag = tagId;
        ViewBag.IsSmartSort = smartSort;
        ViewBag.MyTags = myTags;
        ViewBag.NeedsProfileSetup = !user.IsProfileComplete;
        ViewBag.AllTags = await context.Tags.ToListAsync(); // Needed for the modal/popup

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
            .Include(p => p.Student)
            .Include(p => p.Match)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (proposal == null) return NotFound();

        // If viewed for the first time or returning to it, set to UnderReview
        if (proposal.Status == ProposalStatus.Pending)
        {
            proposal.Status = ProposalStatus.UnderReview;
            context.Notifications.Add(new Notification
            {
                UserId = proposal.StudentId,
                Message = $"Your proposal for module {proposal.Module.Code} is under review!"
            });
            await context.SaveChangesAsync();
        }

        return View(proposal);
    }

    [HttpPost]
    public async Task<IActionResult> RevertToPending(int id)
    {
        var proposal = await context.Proposals.FindAsync(id);
        if (proposal == null) return NotFound();

        if (proposal.Status == ProposalStatus.UnderReview)
        {
            proposal.Status = ProposalStatus.Pending;
            context.Notifications.Add(new Notification
            {
                UserId = proposal.StudentId,
                Message = $"Your proposal for module {proposal.ModuleId} has been returned to pending."
            });
            await context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Dashboard));
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, ProposalStatus status)
    {
        var proposal = await context.Proposals.FindAsync(id);
        if (proposal == null) return NotFound();

        proposal.Status = status;
        await context.SaveChangesAsync();

        // Notification logic
        string msg = status switch
        {
            ProposalStatus.UnderReview => $"Your proposal for module {proposal.ModuleId} is under review!",
            ProposalStatus.Pending => $"Your proposal for module {proposal.ModuleId} has been returned to pending.",
            _ => ""
        };

        if (!string.IsNullOrEmpty(msg))
        {
            context.Notifications.Add(new Notification { UserId = proposal.StudentId, Message = msg });
            await context.SaveChangesAsync();
        }

        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> Match(int proposalId, string message)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var proposal = await context.Proposals.FindAsync(proposalId);
        if (proposal == null) return NotFound();

        proposal.Status = ProposalStatus.Matched;

        var match = new Match
        {
            ProposalId = proposalId,
            SupervisorId = user.Id,
            Message = message
        };

        context.Matches.Add(match);
        context.Notifications.Add(new Notification
        {
            UserId = proposal.StudentId,
            Message = "Your proposal has been matched with a supervisor!"
        });

        await context.SaveChangesAsync();

        return RedirectToAction(nameof(Dashboard));
    }

    public async Task<IActionResult> MyMatches()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Unauthorized();

        var matches = await context.Matches
            .Include(m => m.Proposal)
                .ThenInclude(p => p.Student)
            .Include(m => m.Proposal)
                .ThenInclude(p => p.Module)
            .Where(m => m.SupervisorId == user.Id)
            .ToListAsync();

        return View(matches);
    }
