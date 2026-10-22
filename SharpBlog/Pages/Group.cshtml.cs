using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SharpBlog.Pages;

public class Group(BlogDbContext dbContext) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public required string? TagName { get; set; }
    public required bool Error { get; set; }
    public IEnumerable<Article> ArticlesFallsIntoTag { get; set; } = [];
    public IActionResult OnGet()
    {
        if (TagName is null || !dbContext.Articles.Where(i => !i.Unlisted).Select(i=>i.Tags).AsEnumerable().Any(i=>i.Contains(TagName)))
        {
            HttpContext.Response.StatusCode = 404; 
            Error = true;
            return Page();
        }
        ArticlesFallsIntoTag = dbContext.Articles.Where(i => !i.Unlisted).AsEnumerable().Where(i => i.Tags.Contains(TagName));
        return Page(); 
    }
}