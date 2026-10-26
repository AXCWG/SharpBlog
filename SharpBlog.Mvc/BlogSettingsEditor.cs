using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace SharpBlog.Mvc;

/// <summary>
/// 把 <see cref="CommonSettings"/> 当作配置文件看待：静态属性的初始值就是「源代码里的配置」。
/// 后台只做运行期覆盖（改静态属性），不落盘、不写库，重启或重新编译后回到源代码里的值。
/// 只有带公开 setter 的属性才会出现在后台。
/// </summary>
public static class BlogSettingsEditor
{
    private static readonly MemberInfo[] Properties =
    [
        ..typeof(CommonSettings)

            .GetProperties(BindingFlags.Public | BindingFlags.Static)

            .Where(i => i.GetMethod is not null && i.GetIndexParameters().Length == 0)
            .ToArray(),
        .. typeof(CommonSettings).GetFields(BindingFlags.Static | BindingFlags.Public )
    ];

    /// <summary>后台渲染用的一行设置。</summary>
    public sealed record SettingField(
        string Name,
        Type Type,
        bool Editable,
        object? Value,
        string Kind,
        string Description);

    public static IReadOnlyList<SettingField> Describe() => Properties
        .Select(property =>  property is PropertyInfo s ? new SettingField(
            s.Name,
            s.PropertyType,
            s.SetMethod is { IsPublic: true },
            s.GetValue(null),
            KindOf(s.PropertyType),
            DescribeOf(s)) : property is FieldInfo {IsLiteral: true} f  ? new SettingField(
            f.Name,
            f.FieldType,
            false, f.GetValue(null), KindOf(f.FieldType), "const 属性") : throw new ArgumentOutOfRangeException(nameof(property))) 
        .ToList();

    /// <summary>把提交值写进静态属性，返回错误信息（空表示成功）。不涉及任何持久化。</summary>
    public static IReadOnlyList<string> Apply(IReadOnlyDictionary<string, string?> submitted)
    {
        var errors = new List<string>();
        var pending = new List<(PropertyInfo Property, object? Value)>();

        // 先整体校验，全部通过才落值：避免一半生效、一半报错
        foreach (var property in Properties)
        {
            if (property is PropertyInfo s)
            {
                if (s.SetMethod is not { IsPublic: true })
                {
                    continue;
                }

                if (!submitted.TryGetValue(property.Name, out var raw))
                {
                    continue;
                }

                if (!TryConvert(raw, s.PropertyType, out var value, out var error))
                {
                    errors.Add($"{property.Name}: {error}");
                    continue;
                }
                pending.Add((s, value));
            }
            
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        foreach (var (property, value) in pending)
        {
            property.SetValue(null, value);
        }

        return errors;
    }

    /// <summary>string / bool / number / text，决定后台用什么控件。</summary>
    private static string KindOf(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying == typeof(string)) return "string";
        if (underlying == typeof(bool)) return "bool";
        if (underlying == typeof(int) || underlying == typeof(long) ||
            underlying == typeof(float) || underlying == typeof(double) || underlying == typeof(decimal))
        {
            return "number";
        }

        return "text";
    }

    private static string DescribeOf(PropertyInfo property)
    {
        var display = property.GetCustomAttribute<DisplayAttribute>();
        var description = property.GetCustomAttribute<DescriptionAttribute>();
        return description?.Description ?? display?.Description ?? $"{property.PropertyType.Name} 静态属性";
    }

    private static bool TryConvert(string? raw, Type type, out object? value, out string? error)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        var nullable = Nullable.GetUnderlyingType(type) is not null || type == typeof(string);
        var text = raw?.Trim() ?? string.Empty;

        error = null;

        if (underlying == typeof(string))
        {
            value = raw ?? string.Empty;
            return true;
        }

        if (text.Length == 0)
        {
            value = null;
            if (nullable)
            {
                return true;
            }

            error = "不能为空。";
            return false;
        }

        if (underlying == typeof(bool))
        {
            if (bool.TryParse(text, out var boolean))
            {
                value = boolean;
                return true;
            }

            value = null;
            error = "需要 true 或 false。";
            return false;
        }

        if (underlying == typeof(int))
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                value = number;
                return true;
            }

            value = null;
            error = "需要一个整数。";
            return false;
        }

        try
        {
            value = JsonSerializer.Deserialize(text, type);
            return true;
        }
        catch (Exception exception)
        {
            value = null;
            error = exception.Message;
            return false;
        }
    }
}
