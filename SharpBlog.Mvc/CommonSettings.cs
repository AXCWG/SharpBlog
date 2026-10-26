namespace SharpBlog;

public static class CommonSettings
{
    public static string BlogName { get; set;} = "SharpBlog";
    public static string TitleAppendix { get; set; } = $"{BlogName}";
    public static bool EnableCommentSection { get; set; } = true;
    /// <summary>
    /// Max length of shown post on index. 
    /// </summary>
    public static int? ConcatCount { get; set; } = 10;

    public const  string AdminString = "Admin";
}