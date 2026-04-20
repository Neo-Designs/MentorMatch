using FluentAssertions;
using MentorMatch.Controllers;
using MentorMatch.Models;
using MentorMatch.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace MentorMatch.Tests.Controllers;

public class SupervisorControllerTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public SupervisorControllerTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Details_SetsStatusToUnderReview_AndNotifiesStudent()
    {
        // Arrange
        using var context = _fixture.CreateContext();
        var studentId = "student-x";
        var student = new ApplicationUser { Id = studentId, Email = "studentx@test.com" };
        context.Users.Add(student);

        var supervisorId = "supervisor-y";
        var supervisor = new ApplicationUser { Id = supervisorId, Email = "supy@test.com" };
        context.Users.Add(supervisor);

        var module = await context.Modules.FirstAsync();
        var proposal = new Proposal 
        { 
            Title = "AI Thesis", 
            Abstract = "AI", 
            TechnicalStack = "Py", 
            ResearchArea = "AI", 
            StudentId = studentId, 
            ModuleId = module.Id,
            Status = ProposalStatus.Pending
        };
        context.Proposals.Add(proposal);
        await context.SaveChangesAsync();

        var mockUserManager = MockHelpers.MockUserManager();
        mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(supervisor);

        var mockHubContext = MockHelpers.MockHubContext();
        var controller = new SupervisorController(context, mockUserManager.Object, mockHubContext.Object);
        MockHelpers.SetControllerContext(controller);

        // Act
        var result = await controller.Details(proposal.Id);

        // Assert
        result.Should().BeOfType<ViewResult>();
        proposal.Status.Should().Be(ProposalStatus.UnderReview);

        var notification = await context.Notifications.FirstOrDefaultAsync(n => n.UserId == studentId);
        notification.Should().NotBeNull();
        notification!.Title.Should().Contain("Project Under Review");
    }

    [Fact]
    public async Task Match_CreatesMatchRecord_AndNotifiesStudent()
    {
        // Arrange
        using var context = _fixture.CreateContext();
        var studentId = "student-match";
        var student = new ApplicationUser { Id = studentId, Email = "studentm@test.com" };
        context.Users.Add(student);

        var supervisorId = "supervisor-match";
        var supervisor = new ApplicationUser { Id = supervisorId, Email = "supm@test.com", FirstName = "John", LastName = "Doe" };
        context.Users.Add(supervisor);

        var module = await context.Modules.FirstAsync();
        var proposal = new Proposal 
        { 
            Title = "ML Project", 
            Abstract = "ML", 
            TechnicalStack = "Py", 
            ResearchArea = "ML", 
            StudentId = studentId, 
            ModuleId = module.Id,
            Status = ProposalStatus.UnderReview
        };
        context.Proposals.Add(proposal);
        await context.SaveChangesAsync();

        var mockUserManager = MockHelpers.MockUserManager();
        mockUserManager.Setup(m => m.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .ReturnsAsync(supervisor);

        var mockHubContext = MockHelpers.MockHubContext();
        var controller = new SupervisorController(context, mockUserManager.Object, mockHubContext.Object);
        MockHelpers.SetControllerContext(controller);

        // Act
        var result = await controller.Match(proposal.Id, "Let's work together");

        // Assert
        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be("Dashboard");

        proposal.Status.Should().Be(ProposalStatus.Matched);
        var match = await context.Matches.FirstOrDefaultAsync(m => m.ProposalId == proposal.Id);
        match.Should().NotBeNull();
        match!.SupervisorId.Should().Be(supervisorId);
        match.Message.Should().Be("Let's work together");

        var notification = await context.Notifications.FirstOrDefaultAsync(n => n.UserId == studentId && n.Title.Contains("Match Confirmed"));
        notification.Should().NotBeNull();
    }
}
