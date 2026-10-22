using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace SharpBlog.Pages;

public class Viewer(BlogDbContext dbContext) : PageModel
{
    public required Article? Article { get; set; }
    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }
    public IActionResult OnGet()
    {
        Console.WriteLine(Id);
        if (dbContext.Articles.FirstOrDefault(i => i.Id == Id) is { } article)
        {
            Article = article;
            
        }
        else
        {
            HttpContext.Response.StatusCode = 404;
        }
        return Page();




    }
    
}