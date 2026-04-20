using System.Diagnostics;
using FluentAssertions;
using MentorMatch.Controllers;
using MentorMatch.Data;
using MentorMatch.Models;
using MentorMatch.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace MentorMatch.Tests.Performance;

public class PerformanceTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public PerformanceTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SupervisorDashboard_LoadTest_50ConcurrentRequests_Under200ms()
    {
        // Arrange: Seed 500 proposals and 50 supervisors
        using var context = _fixture.CreateContext();
        await SeedLargeData(context);

        var supervisors = await context.Users
            .Where(u => u.UserType == UserType.Supervisor)
            .Take(50)
            .ToListAsync();

        var hubContext = MockHelpers.MockHubContext();
        var stopWatch = new Stopwatch();

        // Act: Simulate 50 concurrent requests
        stopWatch.Start();
        
        var tasks = supervisors.Select(async sup => 
        {
            var userManager = MockHelpers.MockUserManager();
            userManager.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .ReturnsAsync(sup);

            var controller = new SupervisorController(context, userManager.Object, hubContext.Object);
            MockHelpers.SetControllerContext(controller);

            var result = await controller.Dashboard(null, null, smartSort: true);
            result.Should().BeOfType<ViewResult>();
        });

        await Task.WhenAll(tasks);
        stopWatch.Stop();

        long averageTime = stopWatch.ElapsedMilliseconds / 50;
        
        // Assert
        // We measure average time per request in a concurrent batch
        averageTime.Should().BeLessThan(200, "Dashboard should be highly performant even under load");
        
        Console.WriteLine($"Total time for 50 concurrent requests: {stopWatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Average time per request: {averageTime}ms");
    }

    private async Task SeedLargeData(ApplicationDbContext context)
    {
        // Clear existing to avoid primary key conflicts if run multiple times
        context.Proposals.RemoveRange(context.Proposals);
        context.Users.RemoveRange(context.Users.Where(u => u.Email.Contains("perf")));
        await context.SaveChangesAsync();

        var module = await context.Modules.FirstAsync();
        var student = new ApplicationUser { Id = "p-student", Email = "perf_s@test.com", UserType = UserType.Student };
        context.Users.Add(student);

        // Seed 500 Proposals
        var proposals = Enumerable.Range(1, 500).Select(i => new Proposal
        {
            Title = $"Perf Project {i}",
            Abstract = $"Large scale abstract for project {i} testing LINQ performance.",
            TechnicalStack = "C#, .NET, SQL",
            ResearchArea = "Performance Optimization",
            StudentId = student.Id,
            ModuleId = module.Id,
            CreatedAt = DateTime.UtcNow.AddMinutes(-i)
        }).ToList();
        context.Proposals.AddRange(proposals);

        // Seed 50 Supervisors
        var supervisors = Enumerable.Range(1, 50).Select(i => new ApplicationUser
        {
            Id = $"p-sup-{i}",
            Email = $"perf_sup_{i}@test.com",
            UserType = UserType.Supervisor,
            FirstName = "Supervisor",
            LastName = i.ToString()
        }).ToList();
        context.Users.AddRange(supervisors);

        await context.SaveChangesAsync();
    }
}
