using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharpBlog.Mvc.Models;

namespace SharpBlog.Mvc.Controllers;

[Route(CommonSettings.AdminString)]
public class AdminController(BlogDbContext db) : Controller
{
    private readonly BlogDbContext _db = db;

    [HttpGet("")]
    public IActionResult Index(int? id, string? tab) => View(Build(id, tab));

    // ---------- 保存（新建 / 更新） ----------

    [HttpPost("Save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        [Bind(Prefix = "Form")] AdminViewModel.EditorForm form,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", Build(form.OriginalId > 0 ? form.OriginalId : null, "articles", form));
        }

        var tags = form.ParsedTags();

        if (form.OriginalId > 0)
        {
            var article = await _db.Articles.FirstOrDefaultAsync(i => i.Id == form.OriginalId, cancellationToken);
            if (article is null)
            {
                ModelState.AddModelError(string.Empty, $"文章 #{form.OriginalId} 不存在，可能已被删除。");
                return View("Index", Build(null, "articles", form));
            }

            article.Title = form.Title.Trim();
            article.Content = form.Content;
            article.Tags = tags;
            article.Unlisted = form.Unlisted;
            article.OtherMetadata = form.BuildMetadata();
            await _db.SaveChangesAsync(cancellationToken);

            TempData["Status"] = $"已更新文章 #{article.Id}。";
            return RedirectToAction(nameof(Index), new { id = article.Id });
        }

        var created = new Article
        {
            Title = form.Title.Trim(),
            Content = form.Content,
            Tags = tags,
            Unlisted = form.Unlisted,
            OtherMetadata = form.BuildMetadata(),
            Created = DateTime.Now,
        };

        _db.Articles.Add(created);
        await _db.SaveChangesAsync(cancellationToken);

        TempData["Status"] = $"已创建文章 #{created.Id}。";
        return RedirectToAction(nameof(Index), new { id = created.Id });
    }

    // ---------- 删除文章 ----------

    [HttpPost("DeleteArticle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteArticle(int id, CancellationToken cancellationToken)
    {
        var article = await _db.Articles.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (article is null)
        {
            TempData["Status"] = $"文章 #{id} 不存在。";
            return RedirectToAction(nameof(Index));
        }

        _db.Articles.Remove(article);
        await _db.SaveChangesAsync(cancellationToken);

        TempData["Status"] = $"已删除文章 #{id}（其评论一并删除）。";
        return RedirectToAction(nameof(Index));
    }

    // ---------- 另存为新文章 ----------

    [HttpPost("DuplicateArticle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DuplicateArticle(int id, CancellationToken cancellationToken)
    {
        var source = await _db.Articles.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (source is null)
        {
            TempData["Status"] = $"文章 #{id} 不存在。";
            return RedirectToAction(nameof(Index));
        }

        var copy = new Article
        {
            Title = $"{source.Title}（副本）",
            Content = source.Content,
            Tags = [.. source.Tags],
            OtherMetadata = new Dictionary<string, object>(source.OtherMetadata),
            Unlisted = source.Unlisted,
            Created = DateTime.Now,
        };

        _db.Articles.Add(copy);
        await _db.SaveChangesAsync(cancellationToken);

        TempData["Status"] = $"已复制 #{id} 为 #{copy.Id}。";
        return RedirectToAction(nameof(Index), new { id = copy.Id });
    }

    // ---------- 评论：新增 / 删除 ----------

    [HttpPost("AddComment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(
        [Bind(Prefix = "Comment")] AdminViewModel.CommentForm comment,
        CancellationToken cancellationToken)
    {
        if (!await _db.Articles.AnyAsync(i => i.Id == comment.ArticleId, cancellationToken))
        {
            ModelState.AddModelError(string.Empty, $"文章 #{comment.ArticleId} 不存在。");
        }

        if (!ModelState.IsValid)
        {
            return View("Index", Build(comment.ArticleId, "articles"));
        }

        _db.Comments.Add(new Comment
        {
            ArticleId = comment.ArticleId,
            Username = string.IsNullOrWhiteSpace(comment.Username) ? null : comment.Username.Trim(),
            Content = comment.Content.Trim(),
            Created = DateTime.Now,
        });
        await _db.SaveChangesAsync(cancellationToken);

        TempData["Status"] = "评论已添加。";
        return RedirectToAction(nameof(Index), new { id = comment.ArticleId });
    }

    [HttpPost("DeleteComment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(int commentId, int articleId, CancellationToken cancellationToken)
    {
        var comment = await _db.Comments.FirstOrDefaultAsync(i => i.Id == commentId, cancellationToken);
        if (comment is null)
        {
            TempData["Status"] = $"评论 #{commentId} 不存在。";
            return RedirectToAction(nameof(Index), new { id = articleId });
        }

        _db.Comments.Remove(comment);
        await _db.SaveChangesAsync(cancellationToken);

        TempData["Status"] = $"已删除评论 #{commentId}。";
        return RedirectToAction(nameof(Index), new { id = articleId });
    }

    // ---------- 设置（仅本次运行有效） ----------

    [HttpPost("SaveSettings")]
    [ValidateAntiForgeryToken]
    public IActionResult SaveSettings([Bind(Prefix = "Values")] Dictionary<string, string?> values)
    {
        var errors = BlogSettingsEditor.Apply(values);

        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View("Index", Build(null, "settings"));
        }

        TempData["Status"] = "已应用到本次运行；重启或重新编译后会回到源代码里的值。";
        return RedirectToAction(nameof(Index), new { tab = "settings" });
    }

    // ---------- 视图数据 ----------

    private AdminViewModel Build(int? id, string? tab, AdminViewModel.EditorForm? form = null)
    {
        var model = new AdminViewModel
        {
            Tab = string.Equals(tab, "settings", StringComparison.OrdinalIgnoreCase) ? "settings" : "articles",
            Status = TempData["Status"] as string,
        };

        if (model.Tab == "settings")
        {
            model.SettingFields = BlogSettingsEditor.Describe();
            return model;
        }

        model.Articles = _db.Articles
            .AsNoTracking()
            .OrderByDescending(i => i.Id)
            .Select(i => new AdminViewModel.ArticleSummary(
                i.Id, i.Title, i.Tags, i.Comments.Count, i.Unlisted))
            .ToList();

        if (form is not null)
        {
            model.Form = form;
            model.Comment.ArticleId = form.OriginalId;
            model.Comments = LoadComments(form.OriginalId);
            return model;
        }

        if (id is > 0)
        {
            var article = _db.Articles.AsNoTracking().FirstOrDefault(i => i.Id == id);
            if (article is null)
            {
                ModelState.AddModelError(string.Empty, $"文章 #{id} 不存在。");
                return model;
            }

            model.Form = new AdminViewModel.EditorForm
            {
                OriginalId = article.Id,
                Title = article.Title,
                Content = article.Content,
                Tags = string.Join(", ", article.Tags),
                Unlisted = article.Unlisted,
                Metadata = article.OtherMetadata
                    .Select(kv => new AdminViewModel.MetadataEntry
                    {
                        Key = kv.Key,
                        Value = AdminViewModel.ToEditableString(kv.Value),
                    })
                    .ToList(),
            };
            model.Comment.ArticleId = article.Id;
            model.Comments = LoadComments(article.Id);
        }

        return model;
    }

    private List<AdminViewModel.CommentView> LoadComments(int articleId) => _db.Comments
        .AsNoTracking()
        .Where(i => i.ArticleId == articleId)
        .OrderBy(i => i.Id)
        .Select(i => new AdminViewModel.CommentView(i.Id, i.Username, i.Content, i.Created))
        .ToList();
}
