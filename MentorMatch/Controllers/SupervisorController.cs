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