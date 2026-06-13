using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Portal.Pages;

public class IndexModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Account/Login");
}
