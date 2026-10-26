using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace SharpBlog.Mvc;

/// <summary>
/// 反射式地把 <c>BlogDbContext.Config.ConfigType</c> 暴露给后台：
/// 只有带公开 setter（含 init）的属性会出现，新增配置项不用改页面。
/// 只负责读写这个对象，落库交给调用方（Controller 直接 SaveChanges）。
/// </summary>
public static class BlogSettingsEditor
{
    private static readonly PropertyInfo[] Properties = typeof(BlogDbContext.Config.ConfigType)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(i => i.GetMethod is not null && i.SetMethod is not null)
        .ToArray();

    public sealed record SettingField(
        string Name,
        Type Type,
        object? Value,
        string Kind,
        string Description);

    public static IReadOnlyList<SettingField> Describe(BlogDbContext.Config.ConfigType data) => Properties
        .Select(property => new SettingField(
            property.Name,
            property.PropertyType,
            property.GetValue(data),
            KindOf(property.PropertyType),
            DescribeOf(property)))
        .ToList();

    public static BlogDbContext.Config.ConfigType Clone(BlogDbContext.Config.ConfigType source) =>
        JsonSerializer.Deserialize<BlogDbContext.Config.ConfigType>(
            JsonSerializer.Serialize(source))!;

    /// <summary>先整体校验，全部通过才写值；<paramref name="applyPartial"/> 仅用于校验失败时的回显。</summary>
    public static IReadOnlyList<string> Apply(
        BlogDbContext.Config.ConfigType data,
        IReadOnlyDictionary<string, string?> submitted,
        bool applyPartial = false)
    {
        var errors = new List<string>();
        var pending = new List<(PropertyInfo Property, object? Value)>();

        foreach (var property in Properties)
        {
            if (!submitted.TryGetValue(property.Name, out var raw))
            {
                continue;
            }

            if (!TryConvert(raw, property.PropertyType, out var value, out var error))
            {
                errors.Add($"{property.Name}: {error}");
                continue;
            }

            pending.Add((property, value));
        }

        if (errors.Count > 0 && !applyPartial)
        {
            return errors;
        }

        foreach (var (property, value) in pending)
        {
            property.SetValue(data, value);
        }

        return errors;
    }

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
        return description?.Description ?? display?.Description ?? $"{property.PropertyType.Name} 配置项";
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
