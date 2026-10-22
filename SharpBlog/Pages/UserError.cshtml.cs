using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SharpBlog.Pages;

public class UserError : PageModel
{
    public required string Reason { get; set; }
    public IActionResult OnGet()
    {
        return Page();
    }
}