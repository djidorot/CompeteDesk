using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CompeteDesk.Models;

namespace CompeteDesk.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Strategy> Strategies => Set<Strategy>();

    public DbSet<ActionItem> Actions => Set<ActionItem>();
    // Back-compat alias used by some controllers/views
    public DbSet<ActionItem> ActionItems => Set<ActionItem>();

    public DbSet<WarIntel> WarIntel => Set<WarIntel>();
    public DbSet<WarPlan> WarPlans => Set<WarPlan>();
    public DbSet<WebsiteAnalysisReport> WebsiteAnalysisReports => Set<WebsiteAnalysisReport>();
    public DbSet<BusinessAnalysisReport> BusinessAnalysisReports => Set<BusinessAnalysisReport>();
    public DbSet<DecisionTrace> DecisionTraces => Set<DecisionTrace>();

    // Metrics & Momentum (user-configurable key metrics)
    public DbSet<KeyMetricDefinition> KeyMetricDefinitions => Set<KeyMetricDefinition>();
    public DbSet<KeyMetricEntry> KeyMetricEntries => Set<KeyMetricEntry>();
    public DbSet<Habit> Habits => Set<Habit>();
    public DbSet<HabitCheckin> HabitCheckins => Set<HabitCheckin>();

    // Onboarding
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    // AI/Data controls
    public DbSet<UserAiPreferences> UserAiPreferences => Set<UserAiPreferences>();
    public DbSet<UserDataControls> UserDataControls => Set<UserDataControls>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Workspace>(b =>
        {
            b.Property(x => x.Name).IsRequired().HasMaxLength(120);
            b.Property(x => x.Description).HasMaxLength(1000);

            // Back-compat: some existing SQLite schemas use Workspaces.OwnerUserId (NOT NULL)
            // instead of Workspaces.OwnerId. Keep the domain model property as OwnerId,
            // but map it to the legacy column name.
            b.Property(x => x.OwnerId)
                .HasColumnName("OwnerUserId")
                .IsRequired();

            b.Property(x => x.BusinessType).HasMaxLength(120);
            b.Property(x => x.Country).HasMaxLength(80);
            b.HasIndex(x => new { x.OwnerId, x.Name });
        });

        builder.Entity<Strategy>(b =>
        {
            b.Property(x => x.Name).IsRequired().HasMaxLength(160);
            b.Property(x => x.SourceBook).HasMaxLength(120);
            b.Property(x => x.CorePrinciple).HasMaxLength(300);
            b.Property(x => x.Summary).HasMaxLength(2000);
            b.Property(x => x.Category).HasMaxLength(80);
            b.Property(x => x.Status).IsRequired().HasMaxLength(24);
            b.Property(x => x.AiInsightsJson);
            b.Property(x => x.AiSummary);

            b.HasIndex(x => new { x.OwnerId, x.Status });
            b.HasIndex(x => new { x.WorkspaceId, x.OwnerId });
        });

        builder.Entity<ActionItem>(b =>
        {
            // The physical SQLite table is named "Actions" (created/managed by DbBootstrapper).
            b.ToTable("Actions");
            b.Property(x => x.Title).IsRequired().HasMaxLength(200);
            b.Property(x => x.Description).HasMaxLength(2000);
            b.Property(x => x.Status).IsRequired().HasMaxLength(24);
            b.Property(x => x.Category).HasMaxLength(80);
            b.Property(x => x.SourceBook).HasMaxLength(120);

            b.HasIndex(x => new { x.OwnerId, x.Status });
            b.HasIndex(x => new { x.StrategyId, x.OwnerId });
            b.HasIndex(x => new { x.WorkspaceId, x.OwnerId });
        });

        builder.Entity<KeyMetricDefinition>(b =>
        {
            b.ToTable("KeyMetricDefinitions");
            b.Property(x => x.OwnerId).IsRequired();
            b.Property(x => x.Key).IsRequired().HasMaxLength(48);
            b.Property(x => x.DisplayName).IsRequired().HasMaxLength(80);
            b.Property(x => x.Unit).IsRequired().HasMaxLength(24);
            b.HasIndex(x => new { x.OwnerId, x.Key }).IsUnique();
            b.HasIndex(x => new { x.OwnerId, x.IsEnabled, x.SortOrder });
        });

        builder.Entity<KeyMetricEntry>(b =>
        {
            b.ToTable("KeyMetricEntries");
            b.Property(x => x.OwnerId).IsRequired();
            b.Property(x => x.DateUtc).IsRequired();
            b.Property(x => x.Value).HasColumnType("REAL");
            b.HasIndex(x => new { x.OwnerId, x.DefinitionId, x.DateUtc }).IsUnique();

            b.HasOne(x => x.Definition)
                .WithMany()
                .HasForeignKey(x => x.DefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserAiPreferences>(b =>
        {
            b.ToTable("UserAiPreferences");
            b.Property(x => x.UserId).IsRequired().HasMaxLength(128);
            b.Property(x => x.Verbosity).IsRequired().HasMaxLength(24);
            b.Property(x => x.Tone).IsRequired().HasMaxLength(24);
            b.Property(x => x.AutoDraftPlans).IsRequired();
            b.Property(x => x.AutoSummaries).IsRequired();
            b.Property(x => x.AutoRecommendations).IsRequired();
            b.Property(x => x.StoreDecisionTraces).IsRequired();
            b.Property(x => x.CreatedAtUtc);
            b.Property(x => x.UpdatedAtUtc);
            b.HasIndex(x => x.UserId).IsUnique();
        });

        builder.Entity<UserDataControls>(b =>
        {
            b.ToTable("UserDataControls");
            b.Property(x => x.UserId).IsRequired().HasMaxLength(128);
            b.Property(x => x.RetentionDays).IsRequired();
            b.Property(x => x.ExportFormat).IsRequired().HasMaxLength(16);
            b.Property(x => x.CreatedAtUtc);
            b.Property(x => x.UpdatedAtUtc);
            b.HasIndex(x => x.UserId).IsUnique();
        });

        builder.Entity<UserProfile>(b =>
        {
            b.ToTable("UserProfiles");
            b.Property(x => x.UserId).IsRequired().HasMaxLength(128);
            b.Property(x => x.PersonaRole).IsRequired().HasMaxLength(64);
            b.Property(x => x.PrimaryGoal).HasMaxLength(500);
            b.Property(x => x.CreatedAtUtc);
            b.Property(x => x.UpdatedAtUtc);
            b.HasIndex(x => x.UserId).IsUnique();
        });

        builder.Entity<WarIntel>(b =>
        {
            b.Property(x => x.Title).IsRequired().HasMaxLength(200);
            b.Property(x => x.Subject).HasMaxLength(120);
            b.Property(x => x.Signal).HasMaxLength(2000);
            b.Property(x => x.Source).HasMaxLength(300);
            b.Property(x => x.Tags).HasMaxLength(200);
            b.Property(x => x.Notes).HasMaxLength(4000);

            b.HasIndex(x => new { x.OwnerId, x.Confidence });
            b.HasIndex(x => new { x.WorkspaceId, x.OwnerId });
        });

        builder.Entity<WarPlan>(b =>
        {
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.Objective).HasMaxLength(2000);
            b.Property(x => x.Approach).HasMaxLength(2000);
            b.Property(x => x.Assumptions).HasMaxLength(4000);
            b.Property(x => x.Risks).HasMaxLength(4000);
            b.Property(x => x.Contingencies).HasMaxLength(4000);
            b.Property(x => x.Status).IsRequired().HasMaxLength(24);
            b.Property(x => x.SourceBook).HasMaxLength(120);

            b.HasIndex(x => new { x.OwnerId, x.Status });
            b.HasIndex(x => new { x.WorkspaceId, x.OwnerId });
        });

        builder.Entity<WebsiteAnalysisReport>(b =>
        {
            b.Property(x => x.Url).IsRequired().HasMaxLength(2048);
            b.Property(x => x.FinalUrl).HasMaxLength(512);
            b.Property(x => x.Title).HasMaxLength(512);
            b.Property(x => x.MetaDescription).HasMaxLength(1024);
            b.Property(x => x.AiInsightsJson);
            b.Property(x => x.AiSummary);
            b.Property(x => x.OwnerId).IsRequired();
            b.HasIndex(x => new { x.OwnerId, x.CreatedAtUtc });
            b.HasIndex(x => new { x.OwnerId, x.Url });
            b.HasIndex(x => x.WorkspaceId);
        });

        builder.Entity<BusinessAnalysisReport>(b =>
        {
            b.Property(x => x.OwnerId).IsRequired();
            b.Property(x => x.BusinessType).HasMaxLength(120);
            b.Property(x => x.Country).HasMaxLength(80);
            b.Property(x => x.AiInsightsJson);
            b.HasIndex(x => new { x.OwnerId, x.CreatedAtUtc });
            b.HasIndex(x => x.WorkspaceId);
        });

        builder.Entity<Habit>(b =>
        {
            b.Property(x => x.OwnerId).IsRequired();
            b.Property(x => x.Title).IsRequired().HasMaxLength(200);
            b.Property(x => x.Description).HasMaxLength(2000);
            b.Property(x => x.Frequency).IsRequired().HasMaxLength(16);
            b.HasIndex(x => new { x.OwnerId, x.IsActive });
            b.HasIndex(x => new { x.WorkspaceId, x.OwnerId });
            b.HasIndex(x => new { x.StrategyId, x.OwnerId });
        });

        builder.Entity<HabitCheckin>(b =>
        {
            b.Property(x => x.OwnerId).IsRequired();
            b.Property(x => x.Note).HasMaxLength(500);
            b.HasIndex(x => new { x.OwnerId, x.OccurredOnUtc });
            b.HasIndex(x => new { x.HabitId, x.OwnerId, x.OccurredOnUtc }).IsUnique();
        });

        builder.Entity<DecisionTrace>(b =>
        {
            b.Property(x => x.OwnerId).IsRequired();
            b.Property(x => x.Feature).IsRequired().HasMaxLength(120);
            b.Property(x => x.EntityType).HasMaxLength(80);
            b.Property(x => x.EntityTitle).HasMaxLength(200);
            b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(64);

            b.Property(x => x.InputJson);
            b.Property(x => x.OutputJson);

            b.Property(x => x.AiProvider).HasMaxLength(40);
            b.Property(x => x.Model).HasMaxLength(80);

            b.HasIndex(x => new { x.OwnerId, x.CreatedAtUtc });
            b.HasIndex(x => new { x.WorkspaceId, x.OwnerId });
            b.HasIndex(x => new { x.OwnerId, x.Feature });
        });
    }
}
