using Microsoft.EntityFrameworkCore;
using SharpBlog;
using Z.Expressions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddDbContext<BlogDbContext>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
    .WithStaticAssets();
using (var serviceScope = app.Services.CreateScope())
{
    var blogDbContext = serviceScope.ServiceProvider.GetService<BlogDbContext>();
    if (blogDbContext is null)
    {
        throw new NullReferenceException("BlogDbContext is null. Injection may be failing. ");
    }
    blogDbContext.Database.Migrate();
    if (!blogDbContext.Articles.Any())
    {
        blogDbContext.Articles.Add(new()
        {
            Title = "Test Article",
            // Metadata = new Dictionary<string, object>{ { "Tag", """["test", "lolz"]""" }},
            Tags = ["a", "b"],
            Content = """
                      # This is a test article. 
                      
                      The blog application strictly follows markdown syntax. 
                      """,
        });
        blogDbContext.SaveChanges();
    }
}
app.Run();
