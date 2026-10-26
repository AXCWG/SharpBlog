using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace SharpBlog.Mvc.Models;

public class ViewerViewModel
{
    [ValidateNever]                       // 不回绑、不参与校验，由控制器自己填
    public  BlogDbContext.Article Article { get; set; }
    [Required(ErrorMessage = "请填写名字。")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "评论不能为空。")]
    public string Content { get; set; } = string.Empty;
}
