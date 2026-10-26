using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharpBlog.Mvc.Models;

namespace SharpBlog.Mvc.Controllers;

[Route("/{action=Index}")]
public class IndexController : Controller
{
    private readonly BlogDbContext _dbContext;
    public IndexController(BlogDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public IActionResult Index()
    {
        return View();
    }
    [HttpGet("{id:int}")]
    public IActionResult Viewer(int id)
    {
        var article = _dbContext.Articles
            .Include(i => i.Comments)
            .FirstOrDefault(i=>i.Id == id);
        if (article is null)
        {
            return NotFound();
        }
        return View(new ViewerViewModel
        {
            Article = article
        });
    }
    [HttpPost("{id:int}")]
    public IActionResult UploadComment(int id, ViewerViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.Article = _dbContext.Articles
                .Include(i => i.Comments)
                .First(i => i.Id == id);

            return View(nameof(Viewer), new ViewerViewModel
            {
                Article = _dbContext.Articles
                    .Include(i => i.Comments)
                    .First(i => i.Id == id)
            });      // 保留用户输入
        }

        var x = _dbContext.Articles.FirstOrDefault(i => i.Id == id);
        x?.Comments.Add(new BlogDbContext.Comment
        {
            Content = model.Content,
            Username = model.Name,
            Article = x,
            Created = DateTime.Now
        });
        _dbContext.SaveChanges();
        return RedirectToActionPermanent(nameof(Viewer), new { id });
    }
    
    [HttpGet("{group}")]
    public IActionResult Group(string group)
    {
        return View(model:group);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}