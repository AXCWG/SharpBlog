using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
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
        var article = _dbContext.Articles.FirstOrDefault(i=>i.Id == id);
        if (article is null)
        {
            return NotFound();
        }
        return View(new ViewerViewModel
        {
            Article = article
        });
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