using Microsoft.AspNetCore.Mvc.Testing;
using FluentAssertions;
using Xunit;
using System.Net;

namespace MentorMatch.Tests.Functional;

public class AuthFunctionalTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthFunctionalTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/Admin/Whitelist")]
    [InlineData("/Student/Dashboard")]
    [InlineData("/Supervisor/Dashboard")]
    public async Task Get_SecureEndpoints_RedirectsToLoginForUnauthenticatedUsers(string url)
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/Identity/Account/Login");
    }
}
