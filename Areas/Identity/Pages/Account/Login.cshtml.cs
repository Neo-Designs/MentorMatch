using System.ComponentModel.DataAnnotations;
using MentorMatch.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MentorMatch.Areas.Identity.Pages.Account;

public class LoginModel(
    SignInManager<ApplicationUser> signInManager,
    ILogger<LoginModel> logger,
    UserManager<ApplicationUser> userManager,
    MentorMatch.Data.ApplicationDbContext context) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public IList<AuthenticationScheme> ExternalLogins { get; set; } = new List<AuthenticationScheme>();

    public string? ReturnUrl { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        returnUrl ??= Url.Content("~/");

        // clearing cookies for a clean login
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        ExternalLogins = (await signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        ExternalLogins = (await signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        if (ModelState.IsValid)
        {
            
            var result = await signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                logger.LogInformation("User logged in.");
                return LocalRedirect(returnUrl);
            }
            if (result.RequiresTwoFactor)
            {
                return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
            }
            if (result.IsLockedOut)
            {
                logger.LogWarning("User account locked out.");
                return RedirectToPage("./Lockout");
            }
            else
            {
                // Custom logic for Whitelisted Auto-Registration
                var whitelistEntry = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                    context.PredefinedEmails, e => e.Email == Input.Email && !e.IsUsed);

                if (whitelistEntry != null)
                {
                    // Check if user already exists in AspNetUsers
                    var user = await userManager.FindByEmailAsync(Input.Email);
                    if (user == null)
                    {
                        // Auto-create user
                        var newUser = new ApplicationUser
                        {
                            UserName = Input.Email,
                            Email = Input.Email,
                            FirstName = whitelistEntry.FirstName ?? "New",
                            LastName = whitelistEntry.LastName ?? "User",
                            UserType = whitelistEntry.RoleRequested,
                            EmailConfirmed = true
                        };

                        var createResult = await userManager.CreateAsync(newUser, Input.Password);
                        if (createResult.Succeeded)
                        {
                            await userManager.AddToRoleAsync(newUser, newUser.UserType.ToString());
                            
                            whitelistEntry.IsUsed = true;
                            await context.SaveChangesAsync();

                            await signInManager.SignInAsync(newUser, isPersistent: Input.RememberMe);
                            logger.LogInformation("Whitelisted user created and logged in.");
                            return LocalRedirect(returnUrl);
                        }
                        
                        foreach (var error in createResult.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }
                        return Page();
                    }
                }

                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }
        }

        
        return Page();
    }
}
