using FluentAssertions;
using MentorMatch.Controllers;
using MentorMatch.Models;
using MentorMatch.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using Microsoft.AspNetCore.SignalR;
using MentorMatch.Hubs;

namespace MentorMatch.Tests.Controllers;

public class StudentControllerTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public StudentControllerTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Dashboard_ReturnsProposalsForCurrentStudent()
    {
        // Arrange
        using var context = _fixture.CreateContext();
        var studentId = "student-1";
        var student = new ApplicationUser { Id = studentId, Email = "student1@test.com", UserType = UserType.Student };
        context.Users.Add(student);
        
        var module = await context.Modules.FirstAsync();
        context.Proposals.Add(new Proposal { 
            Title = "My Project", Abstract = "Desc", TechnicalStack = "C#", ResearchArea = "AI", 
            StudentId = studentId, ModuleId = module.Id 
        });
        await context.SaveChangesAsync();

        var mockUserManager = MockHelpers.MockUserManager();
        mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(student);

        var mockHubContext = MockHelpers.MockHubContext();
        var controller = new StudentController(context, mockUserManager.Object, mockHubContext.Object);
        MockHelpers.SetControllerContext(controller);

        // Act
        var result = await controller.Dashboard();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeAssignableTo<IEnumerable<Proposal>>().Subject;
        model.Should().HaveCount(1);
        model.First().Title.Should().Be("My Project");
    }

    [Fact]
    public async Task Submit_Post_CreatesProposalAndNotifiesRelevantSupervisors()
    {
        // Arrange
        using var context = _fixture.CreateContext();
        var studentId = "student-submit";
        var student = new ApplicationUser { Id = studentId, Email = "student_sub@test.com", UserType = UserType.Student };
        context.Users.Add(student);

        var supervisorId = "supervisor-match";
        var supervisor = new ApplicationUser { Id = supervisorId, Email = "sup@test.com", UserType = UserType.Supervisor };
        context.Users.Add(supervisor);

        var tag = await context.Tags.FirstAsync();
        context.UserTags.Add(new ApplicationUserTag { UserId = supervisorId, TagId = tag.Id });
        
        var module = await context.Modules.FirstAsync();
        await context.SaveChangesAsync();

        var mockUserManager = MockHelpers.MockUserManager();
        mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(student);
        mockUserManager.Setup(m => m.GetUsersInRoleAsync("Supervisor"))
            .ReturnsAsync(new List<ApplicationUser> { supervisor });

        var mockHubContext = MockHelpers.MockHubContext();
        var controller = new StudentController(context, mockUserManager.Object, mockHubContext.Object);
        MockHelpers.SetControllerContext(controller);

        var proposal = new Proposal 
        { 
            Title = "New Submission", 
            Abstract = "AI Research", 
            TechnicalStack = "Python", 
            ResearchArea = "AI", 
            ModuleId = module.Id 
        };
        var selectedTags = new[] { tag.Id };

        // Act
        var result = await controller.Submit(proposal, selectedTags, null);

        // Assert
        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be("Dashboard");

        var dbProposal = await context.Proposals.Include(p => p.ProposalTags).FirstOrDefaultAsync(p => p.Title == "New Submission");
        dbProposal.Should().NotBeNull();
        dbProposal!.StudentId.Should().Be(studentId);
        dbProposal.ProposalTags.Should().HaveCount(1);

        // Check notifications
        var notifications = await context.Notifications.Where(n => n.UserId == supervisorId).ToListAsync();
        notifications.Should().HaveCount(1);
        notifications.First().Title.Should().Contain("New Expertise Match");
    }
}
