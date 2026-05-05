using GamePlanner.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace GamePlanner.API.Context;

public class GamePlannerDbContext(DbContextOptions<GamePlannerDbContext> options) : DbContext(options)
{
    public DbSet<Game>              Games              => Set<Game>();
    public DbSet<Genre>             Genres             => Set<Genre>();
    public DbSet<Tag>               Tags               => Set<Tag>();
    public DbSet<Theme>             Themes             => Set<Theme>();
    public DbSet<PlayerPerspective> PlayerPerspectives => Set<PlayerPerspective>();
    public DbSet<GameMode>          GameModes          => Set<GameMode>();
    public DbSet<User>              Users              => Set<User>();
    public DbSet<ImportJob>         ImportJobs         => Set<ImportJob>();
    public DbSet<GameHLTB>          GamesHLTB          => Set<GameHLTB>();
    public DbSet<WorkerStatus>      WorkerStatuses     => Set<WorkerStatus>();
    public DbSet<SyncMetadata>      SyncMetadata       => Set<SyncMetadata>();
    public DbSet<UpcomingGame>      UpcomingGames      => Set<UpcomingGame>();
    public DbSet<NowPlayingGame>    NowPlayingGames    => Set<NowPlayingGame>();
    public DbSet<GameJournalEntry>  GameJournalEntries => Set<GameJournalEntry>();
    public DbSet<TrackedGame>       TrackedGames       => Set<TrackedGame>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<WorkerStatus>().ToTable("WorkerStatus");
        mb.Entity<SyncMetadata>().ToTable("SyncMetadata");
        mb.Entity<ImportJob>().ToTable("ImportJobs");

        mb.Entity<GameJournalEntry>()
            .HasOne(j => j.Game)
            .WithMany()
            .HasForeignKey(j => j.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<NowPlayingGame>()
            .HasOne(n => n.Game)
            .WithMany()
            .HasForeignKey(n => n.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        mb.Entity<Game>()
            .HasOne<User>()
            .WithMany(u => u.Games)
            .HasForeignKey(g => g.UserId)
            .IsRequired(false);


        mb.Entity<Game>()
            .HasMany(g => g.Genres)
            .WithMany(g => g.Games)
            .UsingEntity(j =>
            {
                j.ToTable("GameGenres");
                j.Property<int>("GameId");
                j.Property<int>("GenreId");
                j.HasKey("GameId", "GenreId");
            });

        mb.Entity<Game>()
            .HasMany(g => g.Tags)
            .WithMany(t => t.Games)
            .UsingEntity(j =>
            {
                j.ToTable("GameTags");
                j.Property<int>("GameId");
                j.Property<int>("TagId");
                j.HasKey("GameId", "TagId");
            });

        mb.Entity<Game>()
            .HasMany(g => g.Themes)
            .WithMany(t => t.Games)
            .UsingEntity(j =>
            {
                j.ToTable("GameThemes");
                j.Property<int>("GameId");
                j.Property<int>("ThemeId");
                j.HasKey("GameId", "ThemeId");
            });

        mb.Entity<Game>()
            .HasMany(g => g.PlayerPerspectives)
            .WithMany(p => p.Games)
            .UsingEntity(j =>
            {
                j.ToTable("GamePlayerPerspectives");
                j.Property<int>("GameId");
                j.Property<int>("PlayerPerspectiveId");
                j.HasKey("GameId", "PlayerPerspectiveId");
            });

        mb.Entity<Game>()
            .HasMany(g => g.GameModes)
            .WithMany(m => m.Games)
            .UsingEntity(j =>
            {
                j.ToTable("GameModesRelation");
                j.Property<int>("GameId");
                j.Property<int>("GameModeId");
                j.HasKey("GameId", "GameModeId");
            });
    }
}
