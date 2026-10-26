using JsonProperty;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace SharpBlog.Mvc;

public class BlogDbContext : DbContext
{
    public virtual DbSet<Article>  Articles { get; set; }
    public virtual DbSet<Comment> Comments { get; set; }
    public virtual DbSet<Config> Configs { get; set; }
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
        public DateTime Created { get; set; }
    }

    [PrimaryKey(nameof(Id))]
    public class Config
    {
        public class ConfigType
        {
            public required string BlogName { get; init;} 
            public required string TitleAppendix { get; init; } 
            public required bool EnableCommentSection { get; init; } 
            /// <summary>
            /// Max length of shown post on index. Null for unlimited. 
            /// </summary>
            public required int? ConcatCount { get; init; } = 10;
        }
        public int Id { get; set; }
        [DbJsonSerialize]
        public required ConfigType Configuration { get; set; }
    }
}

