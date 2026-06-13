using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portal.Services;

namespace Portal.Pages.Account;

public class LoginModel : PageModel
{
    private readonly AuthService _auth;

    public LoginModel(AuthService auth) => _auth = auth;

    [BindProperty] public string  Username   { get; set; } = string.Empty;
    [BindProperty] public string  Password   { get; set; } = string.Empty;
    [BindProperty] public bool    RememberMe { get; set; }
    public string? ErrorMessage { get; set; }

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return LocalRedirect("/portal");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (!ModelState.IsValid) return Page();

        var user = await _auth.ValidateAsync(Username, Password);
        if (user == null)
        {
            ErrorMessage = "Invalid username or password.";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserID.ToString()),
            new(ClaimTypes.Name,           user.Username),
            new("FullName",                user.FullName),
            new(ClaimTypes.Role,           user.RoleType),
        };

        var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProps = new AuthenticationProperties
        {
            IsPersistent = RememberMe,
            ExpiresUtc   = RememberMe
                ? DateTimeOffset.UtcNow.AddDays(30)
                : DateTimeOffset.UtcNow.AddHours(8),
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProps);

        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/portal");
    }
}
