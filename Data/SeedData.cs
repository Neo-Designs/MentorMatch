using MentorMatch.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MentorMatch.Data;

public static class SeedData
{
    public static async Task Initialize(IServiceProvider serviceProvider)
    {
        using var context = new ApplicationDbContext(
            serviceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>());

        // Seed Tags
        if (!context.Tags.Any())
        {
            context.Tags.AddRange(
                new Tag { Name = "AI" },
                new Tag { Name = "IOT" },
                new Tag { Name = "Cloud Computing" },
                new Tag { Name = "Web Development" },
                new Tag { Name = "Mobile Apps" },
                new Tag { Name = "Cybersecurity" },
                new Tag { Name = "Data Science" },
                new Tag { Name = "Blockchain" },
                new Tag { Name = "UI/UX" }
            );
        }

        // Seed Modules
        if (!context.Modules.Any())
        {
            context.Modules.AddRange(
                new Module { Name = "Final Year Project", Code = "CS401" },
                new Module { Name = "Advanced Software Engineering", Code = "CS402" },
                new Module { Name = "Machine Learning Systems", Code = "CS403" },
                new Module { Name = "Cyber Security Architectures", Code = "CS404" }
            );
        }

        await context.SaveChangesAsync();

        // Seed Admin User
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        string adminEmail = "admin@mentormatch.com";
        
        // Ensure roles exist
        string[] roles = ["Admin", "Supervisor", "Student"];
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "System",
                LastName = "Admin",
                UserType = UserType.Admin,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(adminUser, "Admin@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }

        // Seed Supervisor
        string supervisorEmail = "supervisor@mentormatch.com";
        if (await userManager.FindByEmailAsync(supervisorEmail) == null)
        {
            var supervisorUser = new ApplicationUser
            {
                UserName = supervisorEmail,
                Email = supervisorEmail,
                FirstName = "David",
                LastName = "Chen",
                UserType = UserType.Supervisor,
                EmailConfirmed = true,
                ContactDetails = "Dept of Computer Science, Room 402"
            };

            var result = await userManager.CreateAsync(supervisorUser, "Supervisor@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(supervisorUser, "Supervisor");
                
                // Add Expertise Tags (AI, Cloud)
                var aiTag = await context.Tags.FirstAsync(t => t.Name == "AI");
                var cloudTag = await context.Tags.FirstAsync(t => t.Name == "Cloud Computing");
                context.UserTags.AddRange(
                    new ApplicationUserTag { UserId = supervisorUser.Id, TagId = aiTag.Id },
                    new ApplicationUserTag { UserId = supervisorUser.Id, TagId = cloudTag.Id }
                );
                await context.SaveChangesAsync();
            }
        }

        // Seed Student & Sample Proposal
        string studentEmail = "student@mentormatch.com";
        if (await userManager.FindByEmailAsync(studentEmail) == null)
        {
            var studentUser = new ApplicationUser
            {
                UserName = studentEmail,
                Email = studentEmail,
                FirstName = "Alice",
                LastName = "Smith",
                UserType = UserType.Student,
                StudentNumber = "S12345678",
                Degree = "BSc Computer Science",
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(studentUser, "Student@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(studentUser, "Student");

                var module = await context.Modules.FirstAsync(m => m.Code == "CS401");
                var aiTag = await context.Tags.FirstAsync(t => t.Name == "AI");
                var dataTag = await context.Tags.FirstAsync(t => t.Name == "Data Science");

                var proposal = new Proposal
                {
                    Title = "Autonomous Greenhouse Monitoring",
                    Abstract = "An AI-driven system for monitoring plant health using computer vision...",
                    TechnicalStack = "Python, PyTorch, AWS",
                    ResearchArea = "Edge Intelligence",
                    StudentId = studentUser.Id,
                    ModuleId = module.Id,
                    Status = ProposalStatus.Pending,
                    CreatedAt = DateTime.UtcNow.AddDays(-1)
                };

                context.Proposals.Add(proposal);
                await context.SaveChangesAsync();

                context.ProposalTags.AddRange(
                    new ProposalTag { ProposalId = proposal.Id, TagId = aiTag.Id },
                    new ProposalTag { ProposalId = proposal.Id, TagId = dataTag.Id }
                );
                await context.SaveChangesAsync();
            }
        }
    }
}
