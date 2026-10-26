using Microsoft.EntityFrameworkCore;
using SharpBlog.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<BlogDbContext>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller}/{action}")
    .WithStaticAssets();

await using (var asyncServiceScope = app.Services.CreateAsyncScope())
{
    if (asyncServiceScope.ServiceProvider.GetService<BlogDbContext>() is {} blogDbContext)
    {
        await blogDbContext.Database.MigrateAsync();
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
                Created = DateTime.Now
            });
            blogDbContext.SaveChanges();
        }
    }
    
}
app.Run();