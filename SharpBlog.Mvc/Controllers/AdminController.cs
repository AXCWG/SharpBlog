using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharpBlog.Mvc.Models;

namespace SharpBlog.Mvc.Controllers;

[Route(Configuration.AdminString)]
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

        var created = new BlogDbContext.Article
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

        var copy = new BlogDbContext.Article
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

        _db.Comments.Add(new BlogDbContext.Comment
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
    public async Task<IActionResult> SaveSettings(
        [Bind(Prefix = "Values")] Dictionary<string, string?> values,
        CancellationToken cancellationToken)
    {
        var row = await _db.Configs.FirstOrDefaultAsync(cancellationToken);
        var isNew = row is null;
        row ??= new BlogDbContext.Config { Configuration = DefaultConfig() };

        // 值转换属性是按引用比较的，必须换一个新实例，EF 才会认为它变了
        var updated = BlogSettingsEditor.Clone(row.Configuration);
        var errors = BlogSettingsEditor.Apply(updated, values);

        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View("Index", BuildConfigPreview(values));
        }

        row.Configuration = updated;

        if (isNew)
        {
            _db.Configs.Add(row);
        }

        await _db.SaveChangesAsync(cancellationToken);

        TempData["Status"] = "设置已保存到数据库。";
        return RedirectToAction(nameof(Index), new { tab = "settings" });
    }

    // ---------- 视图数据 ----------

    private AdminViewModel Build(
        int? id,
        string? tab,
        AdminViewModel.EditorForm? form = null)
    {
        var model = new AdminViewModel
        {
            Tab = string.Equals(tab, "settings", StringComparison.OrdinalIgnoreCase) ? "settings" : "articles",
            Status = TempData["Status"] as string,
            Config = LoadConfig(),
        };

        if (model.Tab == "settings")
        {
            model.SettingFields = BlogSettingsEditor.Describe(model.Config);
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

    /// <summary>从数据库读出唯一那行配置（只读）。</summary>
    private BlogDbContext.Config.ConfigType LoadConfig() =>
        _db.Configs.AsNoTracking().FirstOrDefault()?.Configuration ?? DefaultConfig();

    private static BlogDbContext.Config.ConfigType DefaultConfig() => new()
    {
        BlogName = "SharpBlog",
        TitleAppendix = "SharpBlog",
        EnableCommentSection = true,
        ConcatCount = 10,
    };

    /// <summary>校验失败时用一份克隆回显用户输入，不污染被跟踪的实体。</summary>
    private AdminViewModel BuildConfigPreview(IReadOnlyDictionary<string, string?> values)
    {
        var config = BlogSettingsEditor.Clone(LoadConfig());
        BlogSettingsEditor.Apply(config, values, applyPartial: true);

        return new AdminViewModel
        {
            Tab = "settings",
            Config = config,
            SettingFields = BlogSettingsEditor.Describe(config),
            Status = TempData["Status"] as string,
        };
    }

    private List<AdminViewModel.CommentView> LoadComments(int articleId) => _db.Comments
        .AsNoTracking()
        .Where(i => i.ArticleId == articleId)
        .OrderBy(i => i.Id)
        .Select(i => new AdminViewModel.CommentView(i.Id, i.Username, i.Content, i.Created))
        .ToList();
}
