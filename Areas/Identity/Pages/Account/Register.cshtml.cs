using System.ComponentModel.DataAnnotations;
using MentorMatch.Data;
using MentorMatch.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MentorMatch.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class RegisterModel(
    UserManager<ApplicationUser> userManager,
    IUserStore<ApplicationUser> userStore,
    SignInManager<ApplicationUser> signInManager,
    ApplicationDbContext context,
    ILogger<RegisterModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        // Student Specific
        public string? StudentNumber { get; set; }
        public string? Degree { get; set; }

        // Supervisor Specific
        public string? ContactDetails { get; set; }
    }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        if (ModelState.IsValid)
        {
            // Whitelist Check
            var whitelistEntry = await context.PredefinedEmails
                .FirstOrDefaultAsync(e => e.Email == Input.Email && !e.IsUsed);

            UserType assignedRole;
            
            if (whitelistEntry != null)
            {
                assignedRole = whitelistEntry.RoleRequested;
                whitelistEntry.IsUsed = true;
            }
            else
            {
                // Default to Student if not whitelisted as Admin/Supervisor
                assignedRole = UserType.Student;
            }

            var user = new ApplicationUser 
            { 
                UserName = Input.Email, 
                Email = Input.Email, 
                FirstName = Input.FirstName, 
                LastName = Input.LastName,
                StudentNumber = Input.StudentNumber,
                Degree = Input.Degree,
                ContactDetails = Input.ContactDetails,
                UserType = assignedRole
            };

            var result = await userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                logger.LogInformation("User created a new account with password.");
                
                await userManager.AddToRoleAsync(user, assignedRole.ToString());

                await signInManager.SignInAsync(user, isPersistent: false);
                return LocalRedirect(returnUrl);
            }
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        return Page();
    }
}
