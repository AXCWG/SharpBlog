namespace SharpBlog;

public static class CommonSettings
{
    public static string BlogName = "SharpBlog";
    public static string TitleAppendix { get; } = $"{BlogName}";
    public static bool EnableCommentSection { get; } = true;
    public static int ConcatCount { get; set; } = 10;

}