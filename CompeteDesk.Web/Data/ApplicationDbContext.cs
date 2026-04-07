using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using CompeteDesk.Models;
using CompeteDesk.Models.Common;
using CompeteDesk.Models.Gamification;

namespace CompeteDesk.Data;

public class ApplicationDbContext : IdentityDbContext
{
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private bool _isSavingAudit;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor httpContextAccessor)
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
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

    // Study planner
    public DbSet<StudyPlan> StudyPlans => Set<StudyPlan>();
    public DbSet<StudyPlanItem> StudyPlanItems => Set<StudyPlanItem>();

    // Gamification
    public DbSet<UserGamificationProfile> UserGamificationProfiles => Set<UserGamificationProfile>();
    public DbSet<BadgeDefinition> BadgeDefinitions => Set<BadgeDefinition>();
    public DbSet<UserBadge> UserBadges => Set<UserBadge>();
    public DbSet<XpEvent> XpEvents => Set<XpEvent>();

    // Security + audit
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<EntityChangeHistory> EntityChangeHistories => Set<EntityChangeHistory>();
    public DbSet<WorkspaceInvite> WorkspaceInvites => Set<WorkspaceInvite>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();
    public DbSet<StrategyComment> StrategyComments => Set<StrategyComment>();
    public DbSet<UserFeaturePermission> UserFeaturePermissions => Set<UserFeaturePermission>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ------------------------------------------------------------
        // Global query filters (soft delete)
        // ------------------------------------------------------------
        builder.Entity<Workspace>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<Strategy>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<ActionItem>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<WarIntel>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<WarPlan>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<WebsiteAnalysisReport>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<BusinessAnalysisReport>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<Habit>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<KeyMetricDefinition>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<KeyMetricEntry>().HasQueryFilter(x => !x.IsDeleted);

        builder.Entity<StudyPlan>().HasQueryFilter(x => !x.IsDeleted);
        builder.Entity<StudyPlanItem>().HasQueryFilter(x => !x.IsDeleted);

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
            b.HasIndex(x => new { x.OwnerId, x.IsDeleted });
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
            b.HasIndex(x => new { x.OwnerId, x.IsDeleted, x.Status });

            b.HasOne(x => x.Workspace)
                .WithMany()
                .HasForeignKey(x => x.WorkspaceId)
                .OnDelete(DeleteBehavior.SetNull);
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
            b.HasIndex(x => new { x.OwnerId, x.IsDeleted, x.Status });

            b.HasOne(x => x.Workspace)
                .WithMany()
                .HasForeignKey(x => x.WorkspaceId)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(x => x.Strategy)
                .WithMany()
                .HasForeignKey(x => x.StrategyId)
                .OnDelete(DeleteBehavior.SetNull);
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
            b.HasIndex(x => new { x.OwnerId, x.IsDeleted });
        });

        builder.Entity<KeyMetricEntry>(b =>
        {
            b.ToTable("KeyMetricEntries");
            b.Property(x => x.OwnerId).IsRequired();
            b.Property(x => x.DateUtc).IsRequired();
            b.Property(x => x.Value).HasColumnType("REAL");
            b.HasIndex(x => new { x.OwnerId, x.DefinitionId, x.DateUtc }).IsUnique();
            b.HasIndex(x => new { x.OwnerId, x.IsDeleted, x.DateUtc });

            b.HasOne(x => x.Definition)
                .WithMany()
                .HasForeignKey(x => x.DefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<WarIntel>(b =>
        {
            b.HasOne(x => x.Workspace)
                .WithMany()
                .HasForeignKey(x => x.WorkspaceId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.OwnerId, x.IsDeleted, x.Confidence });
        });

        builder.Entity<WarPlan>(b =>
        {
            b.HasOne(x => x.Workspace)
                .WithMany()
                .HasForeignKey(x => x.WorkspaceId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.OwnerId, x.IsDeleted, x.Status });
        });

        builder.Entity<WebsiteAnalysisReport>(b =>
        {
            b.HasOne(x => x.Workspace)
                .WithMany()
                .HasForeignKey(x => x.WorkspaceId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.OwnerId, x.IsDeleted, x.CreatedAtUtc });
        });

        builder.Entity<BusinessAnalysisReport>(b =>
        {
            b.HasOne(x => x.Workspace)
                .WithMany()
                .HasForeignKey(x => x.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.OwnerId, x.IsDeleted, x.CreatedAtUtc });
        });

        builder.Entity<Habit>(b =>
        {
            b.HasOne(x => x.Workspace)
                .WithMany()
                .HasForeignKey(x => x.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Strategy)
                .WithMany()
                .HasForeignKey(x => x.StrategyId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.OwnerId, x.IsDeleted, x.IsActive });
        });



        builder.Entity<WorkspaceInvite>(b =>
        {
            b.ToTable("WorkspaceInvites");
            b.Property(x => x.Email).IsRequired().HasMaxLength(256);
            b.Property(x => x.Role).IsRequired().HasMaxLength(24);
            b.Property(x => x.Status).IsRequired().HasMaxLength(24);
            b.HasIndex(x => new { x.WorkspaceId, x.Email, x.Status });
            b.HasOne(x => x.Workspace)
                .WithMany()
                .HasForeignKey(x => x.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });


        builder.Entity<UserFeaturePermission>(b =>
        {
            b.ToTable("UserFeaturePermissions");
            b.Property(x => x.UserId).IsRequired();
            b.Property(x => x.PermissionKey).IsRequired().HasMaxLength(128);
            b.HasIndex(x => new { x.UserId, x.PermissionKey }).IsUnique();
        });
        builder.Entity<WorkspaceMember>(b =>
        {
            b.ToTable("WorkspaceMembers");
            b.Property(x => x.UserId).IsRequired();
            b.Property(x => x.UserEmail).HasMaxLength(256);
            b.Property(x => x.Role).IsRequired().HasMaxLength(24);
            b.HasIndex(x => new { x.WorkspaceId, x.UserId }).IsUnique();
            b.HasIndex(x => new { x.UserId, x.Role });
            b.HasOne(x => x.Workspace)
                .WithMany()
                .HasForeignKey(x => x.WorkspaceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<StrategyComment>(b =>
        {
            b.ToTable("StrategyComments");
            b.Property(x => x.OwnerId).IsRequired();
            b.Property(x => x.AuthorUserId).IsRequired();
            b.Property(x => x.AuthorEmail).HasMaxLength(256);
            b.Property(x => x.Body).IsRequired().HasMaxLength(2000);
            b.HasIndex(x => new { x.StrategyId, x.CreatedAtUtc });
            b.HasIndex(x => new { x.WorkspaceId, x.CreatedAtUtc });
            b.HasOne(x => x.Strategy)
                .WithMany()
                .HasForeignKey(x => x.StrategyId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Workspace)
                .WithMany()
                .HasForeignKey(x => x.WorkspaceId)
                .OnDelete(DeleteBehavior.SetNull);
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

        builder.Entity<AuditLog>(b =>
        {
            b.ToTable("AuditLogs");
            b.Property(x => x.Action).IsRequired().HasMaxLength(40);
            b.Property(x => x.EntityType).HasMaxLength(80);
            b.Property(x => x.EntityId).HasMaxLength(128);
            b.Property(x => x.ActorEmail).HasMaxLength(256);
            b.Property(x => x.ActorUserId).HasMaxLength(128);
            b.Property(x => x.OwnerId).HasMaxLength(128);
            b.Property(x => x.IpAddress).HasMaxLength(64);
            b.Property(x => x.UserAgent).HasMaxLength(512);
            b.Property(x => x.Summary).HasMaxLength(400);
            b.HasIndex(x => new { x.OwnerId, x.CreatedAtUtc });
            b.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAtUtc });
        });

        builder.Entity<EntityChangeHistory>(b =>
        {
            b.ToTable("EntityChangeHistories");
            b.Property(x => x.EntityType).IsRequired().HasMaxLength(80);
            b.Property(x => x.EntityId).IsRequired().HasMaxLength(128);
            b.Property(x => x.Action).IsRequired().HasMaxLength(40);
            b.Property(x => x.ActorEmail).HasMaxLength(256);
            b.Property(x => x.ActorUserId).HasMaxLength(128);
            b.Property(x => x.OwnerId).HasMaxLength(128);
            b.HasIndex(x => new { x.OwnerId, x.ChangedAtUtc });
            b.HasIndex(x => new { x.EntityType, x.EntityId, x.ChangedAtUtc });
        });

        builder.Entity<StudyPlan>(b =>
        {
            b.ToTable("StudyPlans");
            b.Property(x => x.OwnerId).IsRequired();
            b.Property(x => x.Title).IsRequired().HasMaxLength(160);
            b.Property(x => x.WeekStartUtc).IsRequired();
            b.Property(x => x.WeeklyMinutesTarget).IsRequired();
            b.Property(x => x.AiRoadmapJson);
            b.HasIndex(x => new { x.OwnerId, x.WorkspaceId, x.WeekStartUtc });
            b.HasIndex(x => new { x.OwnerId, x.IsDeleted, x.WeekStartUtc });
        });

        builder.Entity<StudyPlanItem>(b =>
        {
            b.ToTable("StudyPlanItems");
            b.Property(x => x.OwnerId).IsRequired();
            b.Property(x => x.Title).IsRequired().HasMaxLength(200);
            b.Property(x => x.ItemType).IsRequired().HasMaxLength(32);
            b.Property(x => x.ScheduledOnUtc).IsRequired();
            b.HasIndex(x => new { x.OwnerId, x.StudyPlanId, x.ScheduledOnUtc });
            b.HasIndex(x => new { x.OwnerId, x.IsDeleted, x.ScheduledOnUtc });

            b.HasOne<StudyPlan>()
                .WithMany()
                .HasForeignKey(x => x.StudyPlanId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserGamificationProfile>(b =>
        {
            b.ToTable("UserGamificationProfiles");
            b.Property(x => x.OwnerId).IsRequired();
            b.Property(x => x.Rank).IsRequired().HasMaxLength(40);
            b.HasIndex(x => x.OwnerId).IsUnique();
        });

        builder.Entity<BadgeDefinition>(b =>
        {
            b.ToTable("BadgeDefinitions");
            b.Property(x => x.Key).IsRequired().HasMaxLength(64);
            b.Property(x => x.Name).IsRequired().HasMaxLength(80);
            b.HasIndex(x => x.Key).IsUnique();
        });

        builder.Entity<UserBadge>(b =>
        {
            b.ToTable("UserBadges");
            b.Property(x => x.OwnerId).IsRequired();
            b.Property(x => x.BadgeKey).IsRequired().HasMaxLength(64);
            b.Property(x => x.BadgeName).IsRequired().HasMaxLength(80);
            b.HasIndex(x => new { x.OwnerId, x.BadgeKey }).IsUnique();
            b.HasIndex(x => new { x.OwnerId, x.EarnedAtUtc });
        });

        builder.Entity<XpEvent>(b =>
        {
            b.ToTable("XpEvents");
            b.Property(x => x.OwnerId).IsRequired();
            b.Property(x => x.Reason).IsRequired().HasMaxLength(160);
            b.Property(x => x.SourceType).HasMaxLength(48);
            b.HasIndex(x => new { x.OwnerId, x.OccurredAtUtc });
            b.HasIndex(x => new { x.OwnerId, x.SourceType, x.SourceId });
        });
    }

    public override int SaveChanges()
    {
        return SaveChangesWithAuditAsync(isAsync: false).GetAwaiter().GetResult();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return SaveChangesWithAuditAsync(isAsync: true, cancellationToken);
    }

    private async Task<int> SaveChangesWithAuditAsync(bool isAsync, CancellationToken cancellationToken = default)
    {
        // Avoid recursion when persisting audit rows.
        if (_isSavingAudit)
        {
            ApplyAuditAndSoftDeleteRules();
            return isAsync
                ? await base.SaveChangesAsync(cancellationToken)
                : base.SaveChanges();
        }

        var auditCandidates = CaptureAuditCandidates();

        ApplyAuditAndSoftDeleteRules();

        var result = isAsync
            ? await base.SaveChangesAsync(cancellationToken)
            : base.SaveChanges();

        if (auditCandidates.Count > 0)
        {
            _isSavingAudit = true;
            try
            {
                // After the main save, keys are available (for Added entities too).
                var (auditLogs, historyRows) = MaterializeAuditRows(auditCandidates);

                if (auditLogs.Count > 0) AuditLogs.AddRange(auditLogs);
                if (historyRows.Count > 0) EntityChangeHistories.AddRange(historyRows);

                if (auditLogs.Count > 0 || historyRows.Count > 0)
                {
                    if (isAsync)
                        await base.SaveChangesAsync(cancellationToken);
                    else
                        base.SaveChanges();
                }
            }
            finally
            {
                _isSavingAudit = false;
            }
        }

        return result;
    }

    private sealed record AuditCandidate(
        string Action,
        string EntityType,
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry Entry,
        string? BeforeJson,
        string? AfterJson);

    private List<AuditCandidate> CaptureAuditCandidates()
    {
        var list = new List<AuditCandidate>();

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            // Don't self-audit audit tables.
            if (entry.Entity is AuditLog || entry.Entity is EntityChangeHistory)
                continue;

            // Only audit app/domain entities (skip Identity entities).
            var ns = entry.Entity.GetType().Namespace ?? string.Empty;
            if (!ns.StartsWith("CompeteDesk.Models", StringComparison.Ordinal))
                continue;

            var entityType = entry.Entity.GetType().Name;
            var action = entry.State switch
            {
                EntityState.Added => "Created",
                EntityState.Modified => "Updated",
                EntityState.Deleted => "Deleted",
                _ => "Changed"
            };

            string? before = null;
            string? after = null;

            try
            {
                if (entry.State == EntityState.Modified)
                {
                    before = JsonSerializer.Serialize(entry.OriginalValues.ToObject());
                    after = JsonSerializer.Serialize(entry.CurrentValues.ToObject());
                }
                else if (entry.State == EntityState.Added)
                {
                    after = JsonSerializer.Serialize(entry.CurrentValues.ToObject());
                }
                else if (entry.State == EntityState.Deleted)
                {
                    before = JsonSerializer.Serialize(entry.OriginalValues.ToObject());
                }
            }
            catch
            {
                // Ignore serialization issues; keep audit lightweight.
            }

            list.Add(new AuditCandidate(action, entityType, entry, before, after));
        }

        return list;
    }

    private (List<AuditLog> auditLogs, List<EntityChangeHistory> histories) MaterializeAuditRows(List<AuditCandidate> candidates)
    {
        var auditLogs = new List<AuditLog>();
        var histories = new List<EntityChangeHistory>();

        var now = DateTime.UtcNow;

        var http = _httpContextAccessor?.HttpContext;
        var actorUserId = http?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var actorEmail = http?.User?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                         ?? http?.User?.FindFirst("email")?.Value
                         ?? http?.User?.Identity?.Name;

        var ip = http?.Connection?.RemoteIpAddress?.ToString();
        var ua = http?.Request?.Headers["User-Agent"].ToString();

        foreach (var c in candidates)
        {
            var entityId = "";
            try
            {
                var pk = c.Entry.Metadata.FindPrimaryKey();
                if (pk is not null)
                {
                    var keyValues = pk.Properties.Select(p => c.Entry.Property(p.Name).CurrentValue?.ToString() ?? string.Empty);
                    entityId = string.Join("|", keyValues);
                }
            }
            catch
            {
                // ignore
            }

            // Best-effort owner id resolution (many entities have OwnerId)
            string? ownerId = null;
            try
            {
                var prop = c.Entry.Properties.FirstOrDefault(p => string.Equals(p.Metadata.Name, "OwnerId", StringComparison.OrdinalIgnoreCase));
                ownerId = prop?.CurrentValue?.ToString();
            }
            catch { }

            auditLogs.Add(new AuditLog
            {
                OwnerId = ownerId,
                ActorUserId = actorUserId,
                ActorEmail = actorEmail,
                Action = c.Action,
                EntityType = c.EntityType,
                EntityId = string.IsNullOrWhiteSpace(entityId) ? null : entityId,
                Summary = $"{c.Action} {c.EntityType}",
                IpAddress = ip,
                UserAgent = ua,
                CreatedAtUtc = now
            });

            // Version history per entity
            histories.Add(new EntityChangeHistory
            {
                OwnerId = ownerId,
                ActorUserId = actorUserId,
                ActorEmail = actorEmail,
                EntityType = c.EntityType,
                EntityId = string.IsNullOrWhiteSpace(entityId) ? "" : entityId,
                Action = c.Action,
                BeforeJson = c.BeforeJson,
                AfterJson = c.AfterJson,
                ChangedAtUtc = now
            });
        }

        return (auditLogs, histories);
    }

    private void ApplyAuditAndSoftDeleteRules()
    {
        var now = DateTime.UtcNow;

        var userId = _httpContextAccessor?.HttpContext?.User?
            .FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is IAuditableEntity auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    if (auditable.CreatedAtUtc == default) auditable.CreatedAtUtc = now;
                    auditable.UpdatedAtUtc = now;
                    auditable.CreatedById ??= userId;
                    auditable.UpdatedById ??= userId;
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditable.UpdatedAtUtc = now;
                    auditable.UpdatedById = userId;
                }
            }

            if (entry.Entity is ISoftDeletable soft && entry.State == EntityState.Deleted)
            {
                // Convert hard delete to soft delete.
                entry.State = EntityState.Modified;
                soft.IsDeleted = true;
                soft.DeletedAtUtc = now;
                soft.DeletedById = userId;

                // Keep audit in sync.
                if (entry.Entity is IAuditableEntity aud)
                {
                    aud.UpdatedAtUtc = now;
                    aud.UpdatedById = userId;
                }
            }
        }
    }
}
