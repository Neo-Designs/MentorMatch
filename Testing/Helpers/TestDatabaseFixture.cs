using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MentorMatch.Data;
using MentorMatch.Models;

namespace MentorMatch.Tests.Helpers;

public class TestDatabaseFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    public DbContextOptions<ApplicationDbContext> Options { get; }

    public TestDatabaseFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new ApplicationDbContext(Options);
        context.Database.EnsureCreated();
        
        // Seed some basic data if needed
        SeedTags(context);
        SeedModules(context);
        context.SaveChanges();
    }

    private void SeedTags(ApplicationDbContext context)
    {
        if (!context.Tags.Any())
        {
            context.Tags.AddRange(
                new Tag { Name = "Web Development" },
                new Tag { Name = "Machine Learning" },
                new Tag { Name = "Cybersecurity" }
            );
        }
    }

    private void SeedModules(ApplicationDbContext context)
    {
        if (!context.Modules.Any())
        {
            context.Modules.AddRange(
                new Module { Name = "Final Year Project", Code = "FYP3000" },
                new Module { Name = "Research Project", Code = "RES4000" }
            );
        }
    }

    public ApplicationDbContext CreateContext() => new ApplicationDbContext(Options);

    public void Dispose()
    {
        _connection.Dispose();
    }
}
