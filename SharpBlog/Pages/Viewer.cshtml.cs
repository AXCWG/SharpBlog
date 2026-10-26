using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

namespace SharpBlog.Pages;

public class Viewer(BlogDbContext dbContext) : PageModel
{
    public required Article? Article { get; set; }
    [BindProperty(SupportsGet = true)]
    public int Id { get; set; }
    [BindProperty]
    public Comment AddCommentForm { get; set; }
    
    public IActionResult OnGet()
    {
        Console.WriteLine(Id);
        if (dbContext.Articles.Include(i=>i.Comments).FirstOrDefault(i => i.Id == Id) is { } article)
        {
            Article = article;
            
        }
        else
        {
            HttpContext.Response.StatusCode = 404;
        }
        return Page();
    }

    public IActionResult OnPostAddComment()
    {
        if ((ModelState["AddCommentForm"]?.ValidationState == ModelValidationState.Valid))
        {
            
        }

        return Page();
    }

    public class Comment
    {
        public string Name { get; set; }
        public string Content { get; set; }
    }
}