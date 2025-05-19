#region

using Microsoft.EntityFrameworkCore;

#endregion

namespace imagehub.tables;

public class MyContext : DbContext
{
    public MyContext(DbContextOptions<MyContext> options) : base(options)
    {
    }

    public DbSet<Images> Images { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // modelBuilder.Entity<Product>().HasKey(p => p.Id); // volitelně
    }
}