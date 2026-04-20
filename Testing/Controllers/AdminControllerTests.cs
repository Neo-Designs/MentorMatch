using FluentAssertions;
using MentorMatch.Controllers;
using MentorMatch.Models;
using MentorMatch.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using Microsoft.AspNetCore.Identity;

namespace MentorMatch.Tests.Controllers;

public class AdminControllerTests : IClassFixture<TestDatabaseFixture>
{
    private readonly TestDatabaseFixture _fixture;

    public AdminControllerTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateAccount_Succeeds_AndLinksToWhitelist()
    {
        // Arrange
        using var context = _fixture.CreateContext();
        var admin = new ApplicationUser { Id = "admin-1", Email = "admin@test.com" };
        context.Users.Add(admin);

        var email = "invited@test.com";
        context.PredefinedEmails.Add(new PredefinedEmail { Email = email, RoleRequested = UserType.Supervisor });
        await context.SaveChangesAsync();

        var mockUserManager = MockHelpers.MockUserManager();
        mockUserManager.Setup(m => m.FindByEmailAsync(email)).ReturnsAsync((ApplicationUser)null!);
        mockUserManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        mockUserManager.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var mockHubContext = MockHelpers.MockHubContext();
        var controller = new AdminController(context, mockUserManager.Object, mockHubContext.Object);
        MockHelpers.SetControllerContext(controller);

        // Act
        var result = await controller.CreateAccount(email, "Pass123!", "First", "Last", UserType.Supervisor);

        // Assert
        result.Should().BeOfType<RedirectToActionResult>().Which.ActionName.Should().Be("Whitelist");
        
        var whitelistEntry = await context.PredefinedEmails.FirstOrDefaultAsync(e => e.Email == email);
        whitelistEntry!.IsUsed.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteUser_CleansUpRelatedData()
    {
        // Arrange
        using var context = _fixture.CreateContext();
        var studentId = "student-delete";
        var student = new ApplicationUser { Id = studentId, Email = "del@test.com", UserType = UserType.Student };
        context.Users.Add(student);

        var module = await context.Modules.FirstAsync();
        var proposal = new Proposal { Title = "To Delete", StudentId = studentId, ModuleId = module.Id };
        context.Proposals.Add(proposal);
        await context.SaveChangesAsync();

        var mockUserManager = MockHelpers.MockUserManager();
        mockUserManager.Setup(m => m.FindByIdAsync(studentId)).ReturnsAsync(student);
        mockUserManager.Setup(m => m.GetUserId(It.IsAny<System.Security.Claims.ClaimsPrincipal>())).Returns("admin-id");
        mockUserManager.Setup(m => m.DeleteAsync(student)).ReturnsAsync(IdentityResult.Success);

        var mockHubContext = MockHelpers.MockHubContext();
        var controller = new AdminController(context, mockUserManager.Object, mockHubContext.Object);
        MockHelpers.SetControllerContext(controller);

        // Act
        var result = await controller.DeleteUser(studentId);

        // Assert
        result.Should().BeOfType<RedirectToActionResult>();
        
        var dbProposal = await context.Proposals.FirstOrDefaultAsync(p => p.StudentId == studentId);
        dbProposal.Should().BeNull();
    }

    [Fact]
    public async Task ReassignMatch_CreatesNewMatch_AndNotifiesParties()
    {
        // Arrange
        using var context = _fixture.CreateContext();
        var studentId = "student-r";
        var student = new ApplicationUser { Id = studentId, Email = "sr@test.com" };
        context.Users.Add(student);

        var supervisorId = "sup-new";
        var supervisor = new ApplicationUser { Id = supervisorId, Email = "sn@test.com" };
        context.Users.Add(supervisor);

        var module = await context.Modules.FirstAsync();
        var proposal = new Proposal { Id = 101, Title = "Reassign Me", StudentId = studentId, ModuleId = module.Id, Status = ProposalStatus.Pending };
        context.Proposals.Add(proposal);
        await context.SaveChangesAsync();

        var mockUserManager = MockHelpers.MockUserManager();
        var mockHubContext = MockHelpers.MockHubContext();
        var controller = new AdminController(context, mockUserManager.Object, mockHubContext.Object);
        MockHelpers.SetControllerContext(controller);

        // Act
        var result = await controller.ReassignMatch(proposal.Id, supervisorId);

        // Assert
        result.Should().BeOfType<RedirectToActionResult>();
        proposal.Status.Should().Be(ProposalStatus.Matched);
        
        var match = await context.Matches.FirstOrDefaultAsync(m => m.ProposalId == proposal.Id);
        match!.SupervisorId.Should().Be(supervisorId);

        var studentNoti = await context.Notifications.FirstOrDefaultAsync(n => n.UserId == studentId && n.Title == "ADMIN ALERT");
        studentNoti.Should().NotBeNull();
    }
}
