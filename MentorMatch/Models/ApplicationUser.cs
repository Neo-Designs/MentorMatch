using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace MentorMatch.Models;

public class ApplicationUser : IdentityUser
{
    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    public string? Degree { get; set; }

    public string? StudentNumber { get; set; }

    public string? ContactDetails { get; set; }

    public string? ProfilePicturePath { get; set; }

    [Required]
    public UserType UserType { get; set; }

    // Relationship for Supervisor Expertise
    public virtual ICollection<ApplicationUserTag> ExpertiseTags { get; set; } = new List<ApplicationUserTag>();

    // Relationship for Student Proposals
    public virtual ICollection<Proposal> Proposals { get; set; } = new List<Proposal>();

    // Relationship for Notifications
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public bool IsProfileComplete { get; set; } = false;
}

public class ApplicationUserTag
{
    public string UserId { get; set; } = string.Empty;
    public virtual ApplicationUser User { get; set; } = null!;

    public int TagId { get; set; }
    public virtual Tag Tag { get; set; } = null!;
}
