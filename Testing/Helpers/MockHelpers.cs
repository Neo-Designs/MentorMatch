using Moq;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using MentorMatch.Hubs;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using MentorMatch.Models;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc;

namespace MentorMatch.Tests.Helpers;

public static class MockHelpers
{
    public static void SetControllerContext(Controller controller)
    {
        var httpContext = new DefaultHttpContext();
        var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = tempData;
    }

    public static Mock<UserManager<ApplicationUser>> MockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    public static Mock<IHubContext<NotificationHub>> MockHubContext()
    {
        var mockHubContext = new Mock<IHubContext<NotificationHub>>();
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();

        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        mockClients.Setup(c => c.User(It.IsAny<string>())).Returns(mockClientProxy.Object);
        
        return mockHubContext;
    }

    public static ClaimsPrincipal MockUser(string id, string email, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id),
            new Claim(ClaimTypes.Name, email),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuthType");
        return new ClaimsPrincipal(identity);
    }

    public static IFormFile MockFormFile(string fileName, string content)
    {
        var fileMock = new Mock<IFormFile>();
        var ms = new MemoryStream();
        var writer = new StreamWriter(ms);
        writer.Write(content);
        writer.Flush();
        ms.Position = 0;

        fileMock.Setup(f => f.FileName).Returns(fileName);
        fileMock.Setup(f => f.Length).Returns(ms.Length);
        fileMock.Setup(f => f.OpenReadStream()).Returns(ms);
        fileMock.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((Stream target, CancellationToken ct) => ms.CopyToAsync(target, ct));

        return fileMock.Object;
    }
}
