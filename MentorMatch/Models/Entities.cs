using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MentorMatch.Models;

public class Module
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    public string Code { get; set; } = string.Empty;

    public virtual ICollection<Proposal> Proposals { get; set; } = new List<Proposal>();
}

public class Tag
{
    public int Id { get; set; }
    
    [Required]
    public string Name { get; set; } = string.Empty;

    public virtual ICollection<ProposalTag> ProposalTags { get; set; } = new List<ProposalTag>();
    public virtual ICollection<ApplicationUserTag> UserTags { get; set; } = new List<ApplicationUserTag>();
}

public class Proposal
{
    public int Id { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Abstract { get; set; } = string.Empty;

    [Required]
    public string TechnicalStack { get; set; } = string.Empty;

    [Required]
    public string ResearchArea { get; set; } = string.Empty;

    public string? FilePath { get; set; }

    public ProposalStatus Status { get; set; } = ProposalStatus.Pending;

    public ProjectType ProjectType { get; set; } = ProjectType.Individual;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Foreign Keys
    [Required]
    public string StudentId { get; set; } = string.Empty;
    [ForeignKey("StudentId")]
    public virtual ApplicationUser Student { get; set; } = null!;

    [Required]
    public int ModuleId { get; set; }
    public virtual Module Module { get; set; } = null!;

    public virtual ICollection<ProposalTag> ProposalTags { get; set; } = new List<ProposalTag>();

    public virtual Match? Match { get; set; }
}

public class ProposalTag
{
    public int ProposalId { get; set; }
    public virtual Proposal Proposal { get; set; } = null!;

    public int TagId { get; set; }
    public virtual Tag Tag { get; set; } = null!;
}

public class Match
{
    [Key, ForeignKey("Proposal")]
    public int ProposalId { get; set; }
    public virtual Proposal Proposal { get; set; } = null!;

    [Required]
    public string SupervisorId { get; set; } = string.Empty;
    [ForeignKey("SupervisorId")]
    public virtual ApplicationUser Supervisor { get; set; } = null!;

    public string? Message { get; set; }
    public DateTime MatchedAt { get; set; } = DateTime.UtcNow;
}

public class Notification
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public virtual ApplicationUser User { get; set; } = null!;

    [Required]
    public string Message { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public bool IsRead { get; set; } = false;
}

public class PredefinedEmail
{
    [Key]
    public string Email { get; set; } = string.Empty;

    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    public UserType RoleRequested { get; set; }

    public string? CreatedByAdminId { get; set; }
    
    public bool IsUsed { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
