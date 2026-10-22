using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;
using System.Text.Json;
using JsonProperty;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SharpBlog;

public class BlogDbContext : DbContext
{
    public virtual DbSet<Article>  Articles { get; set; }
    public virtual DbSet<Comment> Comments { get; set; }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder.UseSqlite(
        new SqliteConnectionStringBuilder
        {
            DataSource = "blog.db"
        }.ToString());

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Comment>().HasOne(i => i.Article).WithMany(i => i.Comments).HasForeignKey(i => i.ArticleId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.UseJsonSerializeAttribute<BlogDbContext>();
    }
}

[PrimaryKey(nameof(Id))]
[Index(nameof(Tags))]
public record Article
{
    public int Id { get; set; }
    public required string Title { get; set; }

    // /// <summary>
    // /// Additional property name/property value. 
    // /// </summary>
    // public Dictionary<string, object> Metadata { get; set; } = []; 
    public required string Content { get; set; }

    public ICollection<Comment> Comments { get; set; } = [];
    [DbJsonSerialize]
    public List<string> Tags { get; set; } = [];
    [DbJsonSerialize]
    public Dictionary<string, object> OtherMetadata { get; set; } = []; 
    public DateTime Created { get; set; }
    public bool Unlisted { get; set; }
}

[PrimaryKey(nameof(Id))]
[Index(nameof(Username))]
public record Comment
{
    public int Id { get; set; }
    public required string Content { get; set; }
    /// <summary>
    /// Null for anonymous. 
    /// </summary>
    public required string? Username { get; set; }
    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!; 
}