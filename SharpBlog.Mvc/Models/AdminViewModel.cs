using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using SharpBlog;

namespace SharpBlog.Mvc.Models;

public class AdminViewModel
{
    /// <summary>"articles" | "settings"</summary>
    public string Tab { get; set; } = "articles";

    public EditorForm Form { get; set; } = new();

    public CommentForm Comment { get; set; } = new();

    public IReadOnlyList<ArticleSummary> Articles { get; set; } = [];

    public IReadOnlyList<CommentView> Comments { get; set; } = [];

    public IReadOnlyList<BlogSettingsEditor.SettingField> SettingFields { get; set; } = [];

    public string? Status { get; set; }

    public bool IsEditing => Form.OriginalId > 0;

    public sealed record ArticleSummary(int Id, string Title, IReadOnlyList<string> Tags, int CommentCount, bool Unlisted);

    public sealed record CommentView(int Id, string? Username, string Content, DateTime Created);

    public sealed class EditorForm
    {
        /// <summary>0 = 新建；非 0 = 正在编辑这一篇。</summary>
        public int OriginalId { get; set; }

        [Required(ErrorMessage = "标题不能为空。")]
        [StringLength(200, ErrorMessage = "标题最多 200 个字符。")]
        [Display(Name = "标题")]
        public string Title { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "标签最多 500 个字符。")]
        [Display(Name = "标签")]
        public string Tags { get; set; } = string.Empty;

        [Required(ErrorMessage = "正文不能为空。")]
        [Display(Name = "正文")]
        public string Content { get; set; } = string.Empty;

        [Display(Name = "未列出")]
        public bool Unlisted { get; set; }

        public List<MetadataEntry> Metadata { get; set; } = [];

        private static readonly char[] Separators = [','];

        public List<string> ParsedTags() => Tags
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

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
    }

    public sealed record MetadataEntry
    {
        public string? Key { get; set; }
        public string? Value { get; set; }
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

    /// <summary>把库里的 object 值转成可编辑文本。</summary>
    public static string ToEditableString(object? value) => value switch
    {
        null => string.Empty,
        string text => text,
        JsonElement { ValueKind: JsonValueKind.String } element => element.GetString() ?? string.Empty,
        JsonElement element => element.GetRawText(),
        bool boolean => boolean ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };
}
