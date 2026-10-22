using Microsoft.EntityFrameworkCore;

namespace LQAtools.Server.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Note> Notes => Set<Note>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<ChecklistRow> ChecklistRows => Set<ChecklistRow>();
    public DbSet<TextFilesRow> TextFilesRows => Set<TextFilesRow>();
    public DbSet<ChangelogRow> ChangelogRows => Set<ChangelogRow>();
    public DbSet<BugTrackerRow> BugTrackerRows => Set<BugTrackerRow>();
    public DbSet<CellMarking> CellMarkings => Set<CellMarking>();
    public DbSet<Screenshot> Screenshots => Set<Screenshot>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Note>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.PageName).IsRequired().HasMaxLength(32);
            e.Property(x => x.Title).IsRequired().HasMaxLength(512);
            e.Property(x => x.Category).IsRequired().HasMaxLength(128);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.Property(x => x.CreatedBy).HasMaxLength(256);
            e.Property(x => x.LastModifiedBy).HasMaxLength(256);
            e.HasIndex(x => new { x.PageName, x.ModifiedAtUtc });
        });

        b.Entity<AppUser>(e =>
        {
            e.HasKey(x => x.Name);
            e.Property(x => x.Name).HasMaxLength(256);
        });

        ConfigureRows<ChecklistRow>(b.Entity<ChecklistRow>());
        ConfigureRows<TextFilesRow>(b.Entity<TextFilesRow>());
        ConfigureRows<ChangelogRow>(b.Entity<ChangelogRow>());
        ConfigureRows<BugTrackerRow>(b.Entity<BugTrackerRow>());

        b.Entity<CellMarking>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ColumnProp).IsRequired().HasMaxLength(16);
            e.Property(x => x.Color).IsRequired().HasMaxLength(32);
            e.HasIndex(x => new { x.NoteId, x.RowPosition, x.ColumnProp }).IsUnique();
            e.HasOne<Note>().WithMany().HasForeignKey(x => x.NoteId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Screenshot>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.BugId).IsRequired().HasMaxLength(64);
            e.Property(x => x.Language).IsRequired().HasMaxLength(16);
            e.Property(x => x.FileName).IsRequired().HasMaxLength(512);
            e.Property(x => x.ContentType).IsRequired().HasMaxLength(128);
            e.Property(x => x.ContentHash).IsRequired().HasMaxLength(128);
            e.Property(x => x.BlobKey).IsRequired().HasMaxLength(256);
            e.HasIndex(x => new { x.NoteId, x.BugId, x.Language });
            e.HasOne<Note>().WithMany().HasForeignKey(x => x.NoteId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureRows<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> e)
        where T : class, INoteRow
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.Position);
        e.HasIndex(x => new { x.NoteId, x.Position }).IsUnique();
        e.HasOne<Note>().WithMany().HasForeignKey(x => x.NoteId).OnDelete(DeleteBehavior.Cascade);
    }
}
