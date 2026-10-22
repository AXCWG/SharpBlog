using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace SharpBlog.Pages;

public class Admin(BlogDbContext dbContext) : PageModel
{
    private readonly BlogDbContext _db = dbContext;

    /// <summary>编辑器当前内容（新建或编辑某篇文章）。</summary>
    public EditorForm Form { get; private set; } = new();

    /// <summary>新评论输入框。</summary>
    public CommentForm NewComment { get; private set; } = new();

    public IReadOnlyList<ArticleSummary> ArticleList { get; private set; } = [];
    public IReadOnlyList<CommentView> CommentList { get; private set; } = [];

    /// <summary>true = 正在编辑已有文章，false = 新建。</summary>
    public bool IsEditing => Form.OriginalId > 0;

    public string? Status { get; private set; }

    public async Task OnGetAsync(int? id)
    {
        Status = TempData["Status"] as string;
        await LoadListsAsync(id);

        if (id is > 0)
        {
            await FillEditorAsync(id.Value);
        }
    }

    // ---------- 保存（新建 / 更新） ----------

    public async Task<IActionResult> OnPostSaveAsync(
        [Bind(Prefix = "Form")] EditorForm form,
        CancellationToken cancellationToken)
    {
        Form = form;

        if (!ModelState.IsValid)
        {
            await LoadListsAsync(IsEditing ? Form.OriginalId : null);
            return Page();
        }

        var tags = Form.ParsedTags();

        // 更新已有文章：标题 / 正文 / 标签全量覆盖
        if (IsEditing)
        {
            var article = await _db.Articles.FirstOrDefaultAsync(i => i.Id == Form.OriginalId, cancellationToken);
            if (article is null)
            {
                ModelState.AddModelError(string.Empty, $"文章 #{Form.OriginalId} 不存在，可能已被删除。");
                await LoadListsAsync(null);
                return Page();
            }

            article.Title = Form.Title.Trim();
            article.Content = Form.Content;
            article.Tags = tags;
            article.Unlisted = Form.Unlisted;
            article.OtherMetadata = Form.BuildMetadata();
            await _db.SaveChangesAsync(cancellationToken);

            TempData["Status"] = $"已更新文章 #{article.Id}。";
            return RedirectToPage("/Admin", new { id = article.Id });
        }

        // 新建：Id 由数据库自增
        var created = new Article
        {
            Title = Form.Title.Trim(),
            Content = Form.Content,
            Tags = tags,
            Unlisted = Form.Unlisted,
            OtherMetadata = Form.BuildMetadata(),
        };

        _db.Articles.Add(created);
        await _db.SaveChangesAsync(cancellationToken);

        TempData["Status"] = $"已创建文章 #{created.Id}。";
        return RedirectToPage("/Admin", new { id = created.Id });
    }

    // ---------- 删除文章 ----------

    public async Task<IActionResult> OnPostDeleteArticleAsync(int id, CancellationToken cancellationToken)
    {
        var article = await _db.Articles.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (article is null)
        {
            TempData["Status"] = $"文章 #{id} 不存在。";
            return RedirectToPage("/Admin");
        }

        _db.Articles.Remove(article);
        await _db.SaveChangesAsync(cancellationToken);

        TempData["Status"] = $"已删除文章 #{id}（其评论一并删除）。";
        return RedirectToPage("/Admin");
    }

    // ---------- 另存为新文章 ----------

    public async Task<IActionResult> OnPostDuplicateArticleAsync(int id, CancellationToken cancellationToken)
    {
        var source = await _db.Articles.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (source is null)
        {
            TempData["Status"] = $"文章 #{id} 不存在。";
            return RedirectToPage("/Admin");
        }

        var copy = new Article
        {
            Title = $"{source.Title}（副本）",
            Content = source.Content,
            Tags = [.. source.Tags],
        };

        _db.Articles.Add(copy);
        await _db.SaveChangesAsync(cancellationToken);

        TempData["Status"] = $"已复制 #{id} 为 #{copy.Id}。";
        return RedirectToPage("/Admin", new { id = copy.Id });
    }

    // ---------- 评论：新增 / 删除 ----------

    public async Task<IActionResult> OnPostAddCommentAsync(
        [Bind(Prefix = "NewComment")] CommentForm comment,
        CancellationToken cancellationToken)
    {
        NewComment = comment;

        if (string.IsNullOrWhiteSpace(NewComment.Content))
        {
            ModelState.AddModelError("NewComment.Content", "评论内容不能为空。");
        }

        if (!await _db.Articles.AnyAsync(i => i.Id == NewComment.ArticleId, cancellationToken))
        {
            ModelState.AddModelError(string.Empty, $"文章 #{NewComment.ArticleId} 不存在。");
        }

        if (!ModelState.IsValid)
        {
            await LoadListsAsync(NewComment.ArticleId);
            await FillEditorAsync(NewComment.ArticleId);
            return Page();
        }

        _db.Comments.Add(new Comment
        {
            Content = NewComment.Content.Trim(),
            Username = string.IsNullOrWhiteSpace(NewComment.Username) ? null : NewComment.Username.Trim(),
            ArticleId = NewComment.ArticleId,
        });
        await _db.SaveChangesAsync(cancellationToken);

        TempData["Status"] = "评论已添加。";
        return RedirectToPage("/Admin", new { id = NewComment.ArticleId });
    }

    public async Task<IActionResult> OnPostDeleteCommentAsync(
        int commentId,
        int articleId,
        CancellationToken cancellationToken)
    {
        var comment = await _db.Comments.FirstOrDefaultAsync(i => i.Id == commentId, cancellationToken);
        if (comment is null)
        {
            TempData["Status"] = $"评论 #{commentId} 不存在。";
            return RedirectToPage("/Admin", new { id = articleId });
        }

        _db.Comments.Remove(comment);
        await _db.SaveChangesAsync(cancellationToken);

        TempData["Status"] = $"已删除评论 #{commentId}。";
        return RedirectToPage("/Admin", new { id = articleId });
    }

    // ---------- 读取 ----------

    private async Task LoadListsAsync(int? articleId)
    {
        ArticleList = await _db.Articles
            .AsNoTracking()
            .OrderByDescending(i => i.Id)
            .Select(i => new ArticleSummary(i.Id, i.Title, i.Tags, i.Comments.Count, i.Unlisted))
            .ToListAsync();

        if (articleId is not > 0)
        {
            CommentList = [];
            return;
        }

        CommentList = await _db.Comments
            .AsNoTracking()
            .Where(i => i.ArticleId == articleId)
            .OrderBy(i => i.Id)
            .Select(i => new CommentView(i.Id, i.Username, i.Content))
            .ToListAsync();

        NewComment.ArticleId = articleId.Value;
    }

    private async Task FillEditorAsync(int articleId)
    {
        var article = await _db.Articles
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == articleId);

        if (article is null)
        {
            ModelState.AddModelError(string.Empty, $"文章 #{articleId} 不存在。");
            return;
        }

        Form = new EditorForm
        {
            OriginalId = article.Id,
            Title = article.Title,
            Content = article.Content,
            Tags = string.Join(", ", article.Tags),
            Unlisted = article.Unlisted,
            Metadata = article.OtherMetadata
                .Select(kv => new MetadataEntry { Key = kv.Key, Value = ToEditableString(kv.Value) })
                .ToList(),
        };
    }

    // ---------- 视图模型 ----------

    private static string ToEditableString(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString() ?? string.Empty,
        JsonElement element => element.GetRawText(),
        bool boolean => boolean ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    public sealed record MetadataEntry
    {
        // 允许留空：空 key 的行在 BuildMetadata 里被忽略
        public string? Key { get; set; }
        public string? Value { get; set; }
    }

    public sealed record ArticleSummary(int Id, string Title, IReadOnlyList<string> Tags, int CommentCount, bool Unlisted);

    public sealed record CommentView(int Id, string? Username, string Content);

    public sealed class EditorForm
    {
        /// <summary>0 表示新建；非 0 表示正在编辑这一篇。</summary>
        public int OriginalId { get; set; }

        [Required(ErrorMessage = "标题不能为空。")]
        [StringLength(200, ErrorMessage = "标题最多 200 个字符。")]
        [Display(Name = "标题")]
        public string Title { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "标签最多 500 个字符。")]
        [Display(Name = "标签")]
        public string Tags { get; set; } = string.Empty;

        [Display(Name = "未列出")]
        public bool Unlisted { get; set; }

        /// <summary>OtherMetadata 的可编辑形式：任意多条键值对。</summary>
        public List<MetadataEntry> Metadata { get; set; } = [];

        /// <summary>把当前键值对整理成 Dictionary，空 key 跳过、同名后者覆盖。</summary>
        public Dictionary<string, object> BuildMetadata()
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var entry in Metadata)
            {
                var key = entry.Key?.Trim();
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                result[key] = ParseValue(entry.Value);
            }

            return result;
        }

        /// <summary>
        /// 文本尽量还原成原本的类型：true/false → bool，纯整数/小数 → 数字，其余保持字符串。
        /// 只有「原样写回」才转换，避免 007、1e5 这类被悄悄改写。
        /// </summary>
        private static object ParseValue(string? raw)
        {
            var text = raw ?? string.Empty;
            var trimmed = text.Trim();

            if (bool.TryParse(trimmed, out var boolean) && trimmed is "true" or "false")
            {
                return boolean;
            }

            if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)
                && integer.ToString(CultureInfo.InvariantCulture) == trimmed)
            {
                return integer;
            }

            if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                && number.ToString("R", CultureInfo.InvariantCulture) == trimmed)
            {
                return number;
            }

            return text;
        }

        [Required(ErrorMessage = "正文不能为空。")]
        [Display(Name = "正文")]
        public string Content { get; set; } = string.Empty;

        private static readonly char[] Separators = [','];

        public List<string> ParsedTags() => Tags
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public sealed class CommentForm
    {
        public int ArticleId { get; set; }

        [StringLength(100, ErrorMessage = "昵称最多 100 个字符。")]
        [Display(Name = "昵称")]
        public string? Username { get; set; }

        [Required(ErrorMessage = "评论内容不能为空。")]
        [Display(Name = "评论内容")]
        public string Content { get; set; } = string.Empty;
    }
}
