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