using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MentorMatch.Models;

namespace MentorMatch.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Proposal> Proposals { get; set; }
    public DbSet<Module> Modules { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<ProposalTag> ProposalTags { get; set; }
    public DbSet<ApplicationUserTag> UserTags { get; set; }
    public DbSet<Match> Matches { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<PredefinedEmail> PredefinedEmails { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Many-to-Many for ProposalTags
        builder.Entity<ProposalTag>()
            .HasKey(pt => new { pt.ProposalId, pt.TagId });

        builder.Entity<ProposalTag>()
            .HasOne(pt => pt.Proposal)
            .WithMany(p => p.ProposalTags)
            .HasForeignKey(pt => pt.ProposalId);

        builder.Entity<ProposalTag>()
            .HasOne(pt => pt.Tag)
            .WithMany(t => t.ProposalTags)
            .HasForeignKey(pt => pt.TagId);

        // Many-to-Many for Supervisor Expertise (ApplicationUserTag)
        builder.Entity<ApplicationUserTag>()
            .HasKey(aut => new { aut.UserId, aut.TagId });

        builder.Entity<ApplicationUserTag>()
            .HasOne(aut => aut.User)
            .WithMany(u => u.ExpertiseTags)
            .HasForeignKey(aut => aut.UserId);

        builder.Entity<ApplicationUserTag>()
            .HasOne(aut => aut.Tag)
            .WithMany(t => t.UserTags)
            .HasForeignKey(aut => aut.TagId);

        // 1-to-1 for Match and Proposal
        builder.Entity<Match>()
            .HasKey(m => m.ProposalId);

        builder.Entity<Match>()
            .HasOne(m => m.Proposal)
            .WithOne(p => p.Match)
            .HasForeignKey<Match>(m => m.ProposalId)
            .OnDelete(DeleteBehavior.NoAction); // FIX: Prevents cycle with Proposal

        builder.Entity<Match>()
            .HasOne(m => m.Supervisor)
            .WithMany()
            .HasForeignKey(m => m.SupervisorId)
            .OnDelete(DeleteBehavior.Restrict); // FIX: Prevents cycle with Supervisor
    }
}