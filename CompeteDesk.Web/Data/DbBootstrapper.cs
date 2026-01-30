using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CompeteDesk.Data
{
    public static class DbBootstrapper
    {
        /// <summary>
        /// Create missing tables for the current MVP without requiring EF migrations.
        /// Why: The project currently uses Identity migrations, but MVP tables (Workspaces, Strategies)
        /// are created at runtime so devs can just run the app.
        /// </summary>
        public static async Task EnsureCoreTablesAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await EnsureCoreTablesAsync(db);
        }

        public static async Task EnsureCoreTablesAsync(ApplicationDbContext db)
        {
            // Ensure SQLite file exists and Identity tables exist (via migrations/ensure)
            await db.Database.EnsureCreatedAsync();

            // IMPORTANT:
            // - EnsureCreatedAsync() will NOT update an existing schema.
            // - Your repo may already contain an older app.db with a Workspaces table
            //   missing newer columns (e.g., OwnerId). Creating indexes on missing columns
            //   will crash the app.
            // So: create tables if missing, otherwise "patch" missing columns before indexes.

            await EnsureWorkspacesTableAsync(db);
            await EnsureStrategiesTableAsync(db);
            await EnsureActionsTableAsync(db);
            await EnsureWarIntelTableAsync(db);
            await EnsureWarPlansTableAsync(db);
            await EnsureWebsiteAnalysisReportsTableAsync(db);
            await EnsureBusinessAnalysisReportsTableAsync(db);
            await EnsureKeyMetricsTablesAsync(db);
            await EnsureHabitsTableAsync(db);
            await EnsureHabitCheckinsTableAsync(db);
            await EnsureUserAiPreferencesTableAsync(db);
            await EnsureUserDataControlsTableAsync(db);
            await EnsureUserProfilesTableAsync(db);

            await NormalizeSourceBooksAsync(db);
        }

        private static async Task EnsureWarIntelTableAsync(ApplicationDbContext db)
        {
            if (!await TableExistsAsync(db, "WarIntel"))
            {
                await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE WarIntel (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    WorkspaceId INTEGER NULL,
    OwnerId TEXT NOT NULL,
    Title TEXT NOT NULL,
    Subject TEXT NULL,
    Signal TEXT NULL,
    Source TEXT NULL,
    Confidence INTEGER NOT NULL DEFAULT 3,
    Tags TEXT NULL,
    Notes TEXT NULL,
    ObservedAtUtc TEXT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NULL,
    FOREIGN KEY (WorkspaceId) REFERENCES Workspaces (Id) ON DELETE SET NULL
);");
            }
            else
            {
                await EnsureColumnAsync(db, "WarIntel", "WorkspaceId", "INTEGER", nullable: true);
                await EnsureColumnAsync(db, "WarIntel", "OwnerId", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "WarIntel", "Title", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "WarIntel", "Subject", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "WarIntel", "Signal", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "WarIntel", "Source", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "WarIntel", "Confidence", "INTEGER", nullable: true);
                await EnsureColumnAsync(db, "WarIntel", "Tags", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "WarIntel", "Notes", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "WarIntel", "ObservedAtUtc", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "WarIntel", "CreatedAtUtc", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "WarIntel", "UpdatedAtUtc", "TEXT", nullable: true);
            }

            await db.Database.ExecuteSqlRawAsync(@"
CREATE INDEX IF NOT EXISTS IX_WarIntel_OwnerId_Confidence
ON WarIntel (OwnerId, Confidence);");

            await db.Database.ExecuteSqlRawAsync(@"
CREATE INDEX IF NOT EXISTS IX_WarIntel_WorkspaceId_OwnerId
ON WarIntel (WorkspaceId, OwnerId);");
        }

        private static async Task EnsureWarPlansTableAsync(ApplicationDbContext db)
        {
            if (!await TableExistsAsync(db, "WarPlans"))
            {
                await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE WarPlans (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    WorkspaceId INTEGER NULL,
    OwnerId TEXT NOT NULL,
    Name TEXT NOT NULL,
    Objective TEXT NULL,
    Approach TEXT NULL,
    Assumptions TEXT NULL,
    Risks TEXT NULL,
    Contingencies TEXT NULL,
    Status TEXT NOT NULL,
    StartAtUtc TEXT NULL,
    EndAtUtc TEXT NULL,
    SourceBook TEXT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NULL,
    FOREIGN KEY (WorkspaceId) REFERENCES Workspaces (Id) ON DELETE SET NULL
);");
            }
            else
            {
                await EnsureColumnAsync(db, "WarPlans", "WorkspaceId", "INTEGER", nullable: true);
                await EnsureColumnAsync(db, "WarPlans", "OwnerId", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "WarPlans", "Name", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "WarPlans", "Objective", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "WarPlans", "Approach", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "WarPlans", "Assumptions", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "WarPlans", "Risks", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "WarPlans", "Contingencies", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "WarPlans", "Status", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "WarPlans", "StartAtUtc", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "WarPlans", "EndAtUtc", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "WarPlans", "SourceBook", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "WarPlans", "CreatedAtUtc", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "WarPlans", "UpdatedAtUtc", "TEXT", nullable: true);
            }

            await db.Database.ExecuteSqlRawAsync(@"
CREATE INDEX IF NOT EXISTS IX_WarPlans_OwnerId_Status
ON WarPlans (OwnerId, Status);");

            await db.Database.ExecuteSqlRawAsync(@"
CREATE INDEX IF NOT EXISTS IX_WarPlans_WorkspaceId_OwnerId
ON WarPlans (WorkspaceId, OwnerId);");
        }

        private static async Task EnsureKeyMetricsTablesAsync(ApplicationDbContext db)
        {
            if (!await TableExistsAsync(db, "KeyMetricDefinitions"))
            {
                await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE KeyMetricDefinitions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    OwnerId TEXT NOT NULL,
    Key TEXT NOT NULL,
    DisplayName TEXT NOT NULL,
    Unit TEXT NOT NULL DEFAULT 'number',
    IsEnabled INTEGER NOT NULL DEFAULT 1,
    SortOrder INTEGER NOT NULL DEFAULT 0,
    CreatedAtUtc TEXT NULL,
    UpdatedAtUtc TEXT NULL
);");
            }
            else
            {
                await EnsureColumnAsync(db, "KeyMetricDefinitions", "OwnerId", "TEXT", nullable: false, defaultSql: "''");
                await EnsureColumnAsync(db, "KeyMetricDefinitions", "Key", "TEXT", nullable: false, defaultSql: "''");
                await EnsureColumnAsync(db, "KeyMetricDefinitions", "DisplayName", "TEXT", nullable: false, defaultSql: "''");
                await EnsureColumnAsync(db, "KeyMetricDefinitions", "Unit", "TEXT", nullable: false, defaultSql: "'number'");
                await EnsureColumnAsync(db, "KeyMetricDefinitions", "IsEnabled", "INTEGER", nullable: false, defaultSql: "1");
                await EnsureColumnAsync(db, "KeyMetricDefinitions", "SortOrder", "INTEGER", nullable: false, defaultSql: "0");
                await EnsureColumnAsync(db, "KeyMetricDefinitions", "CreatedAtUtc", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "KeyMetricDefinitions", "UpdatedAtUtc", "TEXT", nullable: true);
            }

            await db.Database.ExecuteSqlRawAsync(@"
CREATE UNIQUE INDEX IF NOT EXISTS IX_KeyMetricDefinitions_OwnerId_Key
ON KeyMetricDefinitions (OwnerId, Key);");

            await db.Database.ExecuteSqlRawAsync(@"
CREATE INDEX IF NOT EXISTS IX_KeyMetricDefinitions_OwnerId_Enabled_Order
ON KeyMetricDefinitions (OwnerId, IsEnabled, SortOrder);");

            if (!await TableExistsAsync(db, "KeyMetricEntries"))
            {
                await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE KeyMetricEntries (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    DefinitionId INTEGER NOT NULL,
    OwnerId TEXT NOT NULL,
    DateUtc TEXT NOT NULL,
    Value REAL NOT NULL DEFAULT 0,
    CreatedAtUtc TEXT NULL,
    UpdatedAtUtc TEXT NULL
);");
            }
            else
            {
                await EnsureColumnAsync(db, "KeyMetricEntries", "DefinitionId", "INTEGER", nullable: false, defaultSql: "0");
                await EnsureColumnAsync(db, "KeyMetricEntries", "OwnerId", "TEXT", nullable: false, defaultSql: "''");
                await EnsureColumnAsync(db, "KeyMetricEntries", "DateUtc", "TEXT", nullable: false, defaultSql: "'1970-01-01'");
                await EnsureColumnAsync(db, "KeyMetricEntries", "Value", "REAL", nullable: false, defaultSql: "0");
                await EnsureColumnAsync(db, "KeyMetricEntries", "CreatedAtUtc", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "KeyMetricEntries", "UpdatedAtUtc", "TEXT", nullable: true);
            }

            await db.Database.ExecuteSqlRawAsync(@"
CREATE UNIQUE INDEX IF NOT EXISTS IX_KeyMetricEntries_Owner_Def_Date
ON KeyMetricEntries (OwnerId, DefinitionId, DateUtc);");

            await db.Database.ExecuteSqlRawAsync(@"
CREATE INDEX IF NOT EXISTS IX_KeyMetricEntries_Owner_Date
ON KeyMetricEntries (OwnerId, DateUtc);");
        }

        private static async Task EnsureWorkspacesTableAsync(ApplicationDbContext db)
        {
            if (!await TableExistsAsync(db, "Workspaces"))
            {
                await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE Workspaces (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Description TEXT NULL,
    -- NOTE: Legacy shipped SQLite uses OwnerUserId (NOT NULL). Keep that column name
    -- and map the C# property Workspace.OwnerId to it in ApplicationDbContext.
    OwnerUserId TEXT NOT NULL,
    BusinessType TEXT NULL,
    Country TEXT NULL,
    BusinessProfileUpdatedAtUtc TEXT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NULL
);");
            }
            else
            {
                // Patch older schemas safely (nullable columns + defaults avoided).
                await EnsureColumnAsync(db, "Workspaces", "Name", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Workspaces", "Description", "TEXT", nullable: true);
                // Support both old/new naming (OwnerId vs OwnerUserId)
                await EnsureColumnAsync(db, "Workspaces", "OwnerUserId", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Workspaces", "OwnerId", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Workspaces", "CreatedAtUtc", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Workspaces", "UpdatedAtUtc", "TEXT", nullable: true);

                // Business profile
                await EnsureColumnAsync(db, "Workspaces", "BusinessType", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Workspaces", "Country", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Workspaces", "BusinessProfileUpdatedAtUtc", "TEXT", nullable: true);

                // If a DB has OwnerId but not OwnerUserId (or vice-versa), copy values so inserts/queries work.
                // SQLite has limited ALTER TABLE, so we avoid trying to change NOT NULL constraints.
                // We just ensure both columns exist and keep them in sync for existing rows.
                await db.Database.ExecuteSqlRawAsync(@"
UPDATE Workspaces
SET OwnerUserId = COALESCE(OwnerUserId, OwnerId)
WHERE OwnerUserId IS NULL;" );

                await db.Database.ExecuteSqlRawAsync(@"
UPDATE Workspaces
SET OwnerId = COALESCE(OwnerId, OwnerUserId)
WHERE OwnerId IS NULL;" );
            }

            // Create indexes AFTER columns exist.
            await db.Database.ExecuteSqlRawAsync(@"
CREATE INDEX IF NOT EXISTS IX_Workspaces_OwnerId_Name
ON Workspaces (OwnerUserId, Name);");
        }

        private static async Task EnsureBusinessAnalysisReportsTableAsync(ApplicationDbContext db)
        {
            if (!await TableExistsAsync(db, "BusinessAnalysisReports"))
            {
                await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE BusinessAnalysisReports (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    WorkspaceId INTEGER NOT NULL,
    OwnerId TEXT NOT NULL,
    BusinessType TEXT NOT NULL,
    Country TEXT NOT NULL,
    AiInsightsJson TEXT NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    FOREIGN KEY (WorkspaceId) REFERENCES Workspaces (Id) ON DELETE CASCADE
);");
            }
            else
            {
                await EnsureColumnAsync(db, "BusinessAnalysisReports", "WorkspaceId", "INTEGER", nullable: true);
                await EnsureColumnAsync(db, "BusinessAnalysisReports", "OwnerId", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "BusinessAnalysisReports", "BusinessType", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "BusinessAnalysisReports", "Country", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "BusinessAnalysisReports", "AiInsightsJson", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "BusinessAnalysisReports", "CreatedAtUtc", "TEXT", nullable: true);
            }

            await db.Database.ExecuteSqlRawAsync(@"
CREATE INDEX IF NOT EXISTS IX_BusinessAnalysisReports_Owner_CreatedAtUtc
ON BusinessAnalysisReports (OwnerId, CreatedAtUtc);");

            await db.Database.ExecuteSqlRawAsync(@"
CREATE INDEX IF NOT EXISTS IX_BusinessAnalysisReports_WorkspaceId
ON BusinessAnalysisReports (WorkspaceId);");
        }


        private static async Task EnsureHabitsTableAsync(ApplicationDbContext db)
        {
            if (!await TableExistsAsync(db, "Habits"))
            {
                await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE Habits (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    WorkspaceId INTEGER NOT NULL,
    StrategyId INTEGER NULL,
    OwnerId TEXT NOT NULL,
    Title TEXT NOT NULL,
    Description TEXT NULL,
    Frequency TEXT NOT NULL,
    TargetCount INTEGER NOT NULL DEFAULT 1,
    IsActive INTEGER NOT NULL DEFAULT 1,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NULL,
    FOREIGN KEY (WorkspaceId) REFERENCES Workspaces (Id) ON DELETE CASCADE,
    FOREIGN KEY (StrategyId) REFERENCES Strategies (Id) ON DELETE SET NULL
);");
            }
            else
            {
                await EnsureColumnAsync(db, "Habits", "WorkspaceId", "INTEGER", nullable: true);
                await EnsureColumnAsync(db, "Habits", "StrategyId", "INTEGER", nullable: true);
                await EnsureColumnAsync(db, "Habits", "OwnerId", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Habits", "Title", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Habits", "Description", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Habits", "Frequency", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Habits", "TargetCount", "INTEGER", nullable: true);
                await EnsureColumnAsync(db, "Habits", "IsActive", "INTEGER", nullable: true);
                await EnsureColumnAsync(db, "Habits", "CreatedAtUtc", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Habits", "UpdatedAtUtc", "TEXT", nullable: true);
            }

            await db.Database.ExecuteSqlRawAsync(@"
CREATE INDEX IF NOT EXISTS IX_Habits_OwnerId_IsActive
ON Habits (OwnerId, IsActive);");

            await db.Database.ExecuteSqlRawAsync(@"
CREATE INDEX IF NOT EXISTS IX_Habits_WorkspaceId_OwnerId
ON Habits (WorkspaceId, OwnerId);");

            await db.Database.ExecuteSqlRawAsync(@"
CREATE INDEX IF NOT EXISTS IX_Habits_StrategyId_OwnerId
ON Habits (StrategyId, OwnerId);");
        }

        private static async Task EnsureHabitCheckinsTableAsync(ApplicationDbContext db)
        {
            if (!await TableExistsAsync(db, "HabitCheckins"))
            {
                await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE HabitCheckins (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    HabitId INTEGER NOT NULL,
    OwnerId TEXT NOT NULL,
    OccurredOnUtc TEXT NOT NULL,
    Count INTEGER NOT NULL DEFAULT 1,
    Note TEXT NULL,
    CreatedAtUtc TEXT NOT NULL,
    FOREIGN KEY (HabitId) REFERENCES Habits (Id) ON DELETE CASCADE
);");
            }
            else
            {
                await EnsureColumnAsync(db, "HabitCheckins", "HabitId", "INTEGER", nullable: true);
                await EnsureColumnAsync(db, "HabitCheckins", "OwnerId", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "HabitCheckins", "OccurredOnUtc", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "HabitCheckins", "Count", "INTEGER", nullable: true);
                await EnsureColumnAsync(db, "HabitCheckins", "Note", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "HabitCheckins", "CreatedAtUtc", "TEXT", nullable: true);
            }

            await db.Database.ExecuteSqlRawAsync(@"
CREATE INDEX IF NOT EXISTS IX_HabitCheckins_OwnerId_OccurredOnUtc
ON HabitCheckins (OwnerId, OccurredOnUtc);");

            // Prevent duplicates for the same habit on the same day.
            await db.Database.ExecuteSqlRawAsync(@"
CREATE UNIQUE INDEX IF NOT EXISTS UX_HabitCheckins_HabitId_OwnerId_OccurredOnUtc
ON HabitCheckins (HabitId, OwnerId, OccurredOnUtc);");
        }

        private static async Task EnsureStrategiesTableAsync(ApplicationDbContext db)
        {
            if (!await TableExistsAsync(db, "Strategies"))
            {
                await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE Strategies (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    WorkspaceId INTEGER NULL,
    OwnerId TEXT NOT NULL,
    Name TEXT NOT NULL,
    SourceBook TEXT NULL,
    CorePrinciple TEXT NULL,
    Summary TEXT NULL,
    Category TEXT NULL,
    StrategyType TEXT NOT NULL DEFAULT 'Growth',
    Status TEXT NOT NULL,
    Priority INTEGER NOT NULL DEFAULT 0,
    AiInsightsJson TEXT NULL,
    AiSummary TEXT NULL,
    AiUpdatedAtUtc TEXT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NULL,
    FOREIGN KEY (WorkspaceId) REFERENCES Workspaces (Id) ON DELETE SET NULL
);");
            }
            else
            {
                await EnsureColumnAsync(db, "Strategies", "WorkspaceId", "INTEGER", nullable: true);
                await EnsureColumnAsync(db, "Strategies", "OwnerId", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Strategies", "Name", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Strategies", "SourceBook", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Strategies", "CorePrinciple", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Strategies", "Summary", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Strategies", "Category", "TEXT", nullable: true);
                // StrategyType is NON-NULL in the EF model. When adding columns to an existing SQLite DB,
                // we must add it with NOT NULL + DEFAULT so legacy rows don't end up with NULL and crash
                // materialization (SqliteValueReader.GetString on NULL).
                await EnsureColumnAsync(db, "Strategies", "StrategyType", "TEXT", nullable: false, defaultSql: "'Growth'");
                await EnsureColumnAsync(db, "Strategies", "Status", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Strategies", "Priority", "INTEGER", nullable: true);
                await EnsureColumnAsync(db, "Strategies", "AiInsightsJson", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Strategies", "AiSummary", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Strategies", "AiUpdatedAtUtc", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Strategies", "CreatedAtUtc", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Strategies", "UpdatedAtUtc", "TEXT", nullable: true);
            }

            // Backfill legacy rows if the column was previously added as NULL.
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE Strategies SET StrategyType = 'Growth' WHERE StrategyType IS NULL OR TRIM(StrategyType) = '';");

            await db.Database.ExecuteSqlRawAsync(@"
CREATE INDEX IF NOT EXISTS IX_Strategies_OwnerId_Status
ON Strategies (OwnerId, Status);");

            await db.Database.ExecuteSqlRawAsync(@"
CREATE INDEX IF NOT EXISTS IX_Strategies_WorkspaceId_OwnerId
ON Strategies (WorkspaceId, OwnerId);");
        }

        private static async Task EnsureActionsTableAsync(ApplicationDbContext db)
        {
            if (!await TableExistsAsync(db, "Actions"))
            {
                await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE Actions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    WorkspaceId INTEGER NULL,
    StrategyId INTEGER NULL,
    OwnerId TEXT NOT NULL,
    Title TEXT NOT NULL,
    Description TEXT NULL,
    Category TEXT NULL,
    Status TEXT NOT NULL,
    Priority INTEGER NOT NULL DEFAULT 0,
    DueAtUtc TEXT NULL,
    SourceBook TEXT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NULL,
    FOREIGN KEY (WorkspaceId) REFERENCES Workspaces (Id) ON DELETE SET NULL,
    FOREIGN KEY (StrategyId) REFERENCES Strategies (Id) ON DELETE SET NULL
);");
            }
            else
            {
                await EnsureColumnAsync(db, "Actions", "WorkspaceId", "INTEGER", nullable: true);
                await EnsureColumnAsync(db, "Actions", "StrategyId", "INTEGER", nullable: true);
                await EnsureColumnAsync(db, "Actions", "OwnerId", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Actions", "Title", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Actions", "Description", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Actions", "Category", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Actions", "Status", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Actions", "Priority", "INTEGER", nullable: true);
                await EnsureColumnAsync(db, "Actions", "DueAtUtc", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Actions", "SourceBook", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Actions", "CreatedAtUtc", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "Actions", "UpdatedAtUtc", "TEXT", nullable: true);
            }

            await db.Database.ExecuteSqlRawAsync(@"
CREATE INDEX IF NOT EXISTS IX_Actions_OwnerId_Status
ON Actions (OwnerId, Status);");

            await db.Database.ExecuteSqlRawAsync(@"
CREATE INDEX IF NOT EXISTS IX_Actions_StrategyId_OwnerId
ON Actions (StrategyId, OwnerId);");

            await db.Database.ExecuteSqlRawAsync(@"
CREATE INDEX IF NOT EXISTS IX_Actions_WorkspaceId_OwnerId
ON Actions (WorkspaceId, OwnerId);");
        }

        private static async Task<bool> TableExistsAsync(ApplicationDbContext db, string tableName)
        {
            // sqlite_master query is safe and fast.
            var conn = (SqliteConnection)db.Database.GetDbConnection();
            await db.Database.OpenConnectionAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$name LIMIT 1;";
                cmd.Parameters.AddWithValue("$name", tableName);
                var result = await cmd.ExecuteScalarAsync();
                return result != null;
            }
            finally
            {
                await db.Database.CloseConnectionAsync();
            }
        }

        private static async Task EnsureColumnAsync(
            ApplicationDbContext db,
            string tableName,
            string columnName,
            string sqliteType,
            bool nullable,
            string? defaultSql = null)
        {
            var conn = (SqliteConnection)db.Database.GetDbConnection();
            await db.Database.OpenConnectionAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"PRAGMA table_info(\"{tableName}\");";
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var name = reader.GetString(reader.GetOrdinal("name"));
                    if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                        return;
                }
            }
            finally
            {
                await db.Database.CloseConnectionAsync();
            }

            // If missing, add it.
            // NOTE: SQLite has limited ALTER TABLE support; ADD COLUMN is supported.
            // SQLite allows NOT NULL only if a DEFAULT is provided when adding a column.
// If caller requests NOT NULL but no default is supplied, we fall back to NULL to avoid migration failures on existing DBs.
var canBeNotNull = !nullable && !string.IsNullOrWhiteSpace(defaultSql);
var nullSql = canBeNotNull ? "NOT NULL" : "NULL";
var defaultClause = canBeNotNull ? $" DEFAULT {defaultSql}" : "";

// WARNING EF1002 (SQL injection): table/column names are controlled by code (not user input),
// so it's acceptable in this bootstrapper.
await db.Database.ExecuteSqlRawAsync(
    $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {sqliteType} {nullSql}{defaultClause};");
        }

        // -----------------------------------------------------------------------------
        // Missing helper(s): EnsureIndexAsync
        // -----------------------------------------------------------------------------
        private static Task EnsureIndexAsync(ApplicationDbContext db, string indexName, string tableName, string columnsSql)
            => EnsureIndexAsync(db, indexName, tableName, columnsSql, CancellationToken.None);

        private static async Task EnsureIndexAsync(
            ApplicationDbContext db,
            string indexName,
            string tableName,
            string columnsSql,
            CancellationToken ct)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (string.IsNullOrWhiteSpace(indexName)) throw new ArgumentException("Index name is required.", nameof(indexName));
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("Table name is required.", nameof(tableName));
            if (string.IsNullOrWhiteSpace(columnsSql)) throw new ArgumentException("Columns SQL is required.", nameof(columnsSql));

            // indexName/tableName/columnsSql are constants from code, not user-input.
            var sql = $"CREATE INDEX IF NOT EXISTS \"{indexName}\" ON \"{tableName}\" {columnsSql};";
            await db.Database.ExecuteSqlRawAsync(sql, ct);
        }

        private static async Task EnsureWebsiteAnalysisReportsTableAsync(ApplicationDbContext db)
        {
            if (!await TableExistsAsync(db, "WebsiteAnalysisReports"))
            {
                await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE WebsiteAnalysisReports (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    WorkspaceId INTEGER NULL,
    OwnerId TEXT NOT NULL,
    Url TEXT NOT NULL,
    FinalUrl TEXT NULL,
    HttpStatusCode INTEGER NOT NULL DEFAULT 0,
    ResponseTimeMs INTEGER NOT NULL DEFAULT 0,
    Title TEXT NULL,
    MetaDescription TEXT NULL,
    WordCount INTEGER NOT NULL DEFAULT 0,
    H1Count INTEGER NOT NULL DEFAULT 0,
    H2Count INTEGER NOT NULL DEFAULT 0,
    InternalLinkCount INTEGER NOT NULL DEFAULT 0,
    ExternalLinkCount INTEGER NOT NULL DEFAULT 0,
    ImageCount INTEGER NOT NULL DEFAULT 0,
    ImagesMissingAltCount INTEGER NOT NULL DEFAULT 0,
    IsHttps INTEGER NOT NULL DEFAULT 0,
    HasViewportMeta INTEGER NOT NULL DEFAULT 0,
    HasRobotsNoindex INTEGER NOT NULL DEFAULT 0,
    HasCanonicalLink INTEGER NOT NULL DEFAULT 0,
    HasOpenGraph INTEGER NOT NULL DEFAULT 0,
    HasCspHeader INTEGER NOT NULL DEFAULT 0,
    HasHstsHeader INTEGER NOT NULL DEFAULT 0,
    AiInsightsJson TEXT NULL,
    AiSummary TEXT NULL,
    CreatedAtUtc TEXT NOT NULL,
    FOREIGN KEY (WorkspaceId) REFERENCES Workspaces (Id) ON DELETE SET NULL
);");

                // Helpful indexes for listing per user.
                await EnsureIndexAsync(db, "IX_WebsiteAnalysisReports_Owner_CreatedAtUtc",
                    "WebsiteAnalysisReports", "(OwnerId, CreatedAtUtc)");
                await EnsureIndexAsync(db, "IX_WebsiteAnalysisReports_Owner_Url",
                    "WebsiteAnalysisReports", "(OwnerId, Url)");
            }
            else
            {
                // Patch older schemas (if the table exists but is missing columns).
                await EnsureColumnAsync(db, "WebsiteAnalysisReports", "WorkspaceId", "INTEGER", true);
                await EnsureColumnAsync(db, "WebsiteAnalysisReports", "OwnerId", "TEXT", false);
                await EnsureColumnAsync(db, "WebsiteAnalysisReports", "Url", "TEXT", false);
                await EnsureColumnAsync(db, "WebsiteAnalysisReports", "FinalUrl", "TEXT", true);
                await EnsureColumnAsync(db, "WebsiteAnalysisReports", "HttpStatusCode", "INTEGER", false);
                await EnsureColumnAsync(db, "WebsiteAnalysisReports", "ResponseTimeMs", "INTEGER", false);
                await EnsureColumnAsync(db, "WebsiteAnalysisReports", "Title", "TEXT", true);
                await EnsureColumnAsync(db, "WebsiteAnalysisReports", "MetaDescription", "TEXT", true);
                await EnsureColumnAsync(db, "WebsiteAnalysisReports", "WordCount", "INTEGER", false);
                await EnsureColumnAsync(db, "WebsiteAnalysisReports", "H1Count", "INTEGER", false);
                await EnsureColumnAsync(db, "WebsiteAnalysisReports", "H2Count", "INTEGER", false);
                await EnsureColumnAsync(db, "WebsiteAnalysisReports", "InternalLinkCount", "INTEGER", false);
                await EnsureColumnAsync(db, "WebsiteAnalysisReports", "ExternalLinkCount", "INTEGER", false);
                await EnsureColumnAsync(db, "WebsiteAnalysisReports", "ImageCount", "INTEGER", false);
                await EnsureColumnAsync(db, "WebsiteAnalysisReports", "ImagesMissingAltCount", "INTEGER", false);
                await EnsureColumnAsync(db, "WebsiteAnalysisReports", "IsHttps", "INTEGER", false);
                await EnsureColumnAsync(db, "WebsiteAnalysisReports", "HasViewportMeta", "INTEGER", false);
                await EnsureColumnAsync(db, "WebsiteAnalysisReports", "HasRobotsNoindex", "INTEGER", false);
                await EnsureColumnAsync(db, "WebsiteAnalysisReports", "HasCanonicalLink", "INTEGER", false);
                await EnsureColumnAsync(db, "WebsiteAnalysisReports", "HasOpenGraph", "INTEGER", false);
                await EnsureColumnAsync(db, "WebsiteAnalysisReports", "HasCspHeader", "INTEGER", false);
                await EnsureColumnAsync(db, "WebsiteAnalysisReports", "HasHstsHeader", "INTEGER", false);
                await EnsureColumnAsync(db, "WebsiteAnalysisReports", "AiInsightsJson", "TEXT", true);
                await EnsureColumnAsync(db, "WebsiteAnalysisReports", "AiSummary", "TEXT", true);
                await EnsureColumnAsync(db, "WebsiteAnalysisReports", "CreatedAtUtc", "TEXT", false);

                await EnsureIndexAsync(db, "IX_WebsiteAnalysisReports_Owner_CreatedAtUtc",
                    "WebsiteAnalysisReports", "(OwnerId, CreatedAtUtc)");
                await EnsureIndexAsync(db, "IX_WebsiteAnalysisReports_Owner_Url",
                    "WebsiteAnalysisReports", "(OwnerId, Url)");
            }
        }


        


        private static async Task EnsureUserAiPreferencesTableAsync(ApplicationDbContext db)
        {
            if (!await TableExistsAsync(db, "UserAiPreferences"))
            {
                await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE UserAiPreferences (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId TEXT NOT NULL,
    Verbosity TEXT NOT NULL DEFAULT 'Balanced',
    Tone TEXT NOT NULL DEFAULT 'Analytical',
    AutoDraftPlans INTEGER NOT NULL DEFAULT 1,
    AutoSummaries INTEGER NOT NULL DEFAULT 1,
    AutoRecommendations INTEGER NOT NULL DEFAULT 1,
    StoreDecisionTraces INTEGER NOT NULL DEFAULT 1,
    CreatedAtUtc TEXT NULL,
    UpdatedAtUtc TEXT NULL
);");
            }
            else
            {
                await EnsureColumnAsync(db, "UserAiPreferences", "UserId", "TEXT", nullable: false, defaultSql: "''");
                await EnsureColumnAsync(db, "UserAiPreferences", "Verbosity", "TEXT", nullable: false, defaultSql: "'Balanced'");
                await EnsureColumnAsync(db, "UserAiPreferences", "Tone", "TEXT", nullable: false, defaultSql: "'Analytical'");
                await EnsureColumnAsync(db, "UserAiPreferences", "AutoDraftPlans", "INTEGER", nullable: false, defaultSql: "1");
                await EnsureColumnAsync(db, "UserAiPreferences", "AutoSummaries", "INTEGER", nullable: false, defaultSql: "1");
                await EnsureColumnAsync(db, "UserAiPreferences", "AutoRecommendations", "INTEGER", nullable: false, defaultSql: "1");
                await EnsureColumnAsync(db, "UserAiPreferences", "StoreDecisionTraces", "INTEGER", nullable: false, defaultSql: "1");
                await EnsureColumnAsync(db, "UserAiPreferences", "CreatedAtUtc", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "UserAiPreferences", "UpdatedAtUtc", "TEXT", nullable: true);
            }

            await db.Database.ExecuteSqlRawAsync(@"
CREATE UNIQUE INDEX IF NOT EXISTS IX_UserAiPreferences_UserId
ON UserAiPreferences (UserId);");
        }

        private static async Task EnsureUserDataControlsTableAsync(ApplicationDbContext db)
        {
            if (!await TableExistsAsync(db, "UserDataControls"))
            {
                await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE UserDataControls (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId TEXT NOT NULL,
    RetentionDays INTEGER NOT NULL DEFAULT 90,
    ExportFormat TEXT NOT NULL DEFAULT 'json',
    CreatedAtUtc TEXT NULL,
    UpdatedAtUtc TEXT NULL
);");
            }
            else
            {
                await EnsureColumnAsync(db, "UserDataControls", "UserId", "TEXT", nullable: false, defaultSql: "''");
                await EnsureColumnAsync(db, "UserDataControls", "RetentionDays", "INTEGER", nullable: false, defaultSql: "90");
                await EnsureColumnAsync(db, "UserDataControls", "ExportFormat", "TEXT", nullable: false, defaultSql: "'json'");
                await EnsureColumnAsync(db, "UserDataControls", "CreatedAtUtc", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "UserDataControls", "UpdatedAtUtc", "TEXT", nullable: true);
            }

            // IMPORTANT: columnsSql must include parentheses for valid SQLite syntax.
            await EnsureIndexAsync(db, "IX_UserDataControls_UserId", "UserDataControls", "(\"UserId\")");
        }

        private static async Task EnsureUserProfilesTableAsync(ApplicationDbContext db)
        {
            if (!await TableExistsAsync(db, "UserProfiles"))
            {
                await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE UserProfiles (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId TEXT NOT NULL,
    PersonaRole TEXT NOT NULL DEFAULT 'Business Owner',
    PrimaryGoal TEXT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NULL
);");
            }
            else
            {
                await EnsureColumnAsync(db, "UserProfiles", "UserId", "TEXT", nullable: false, defaultSql: "''");
                await EnsureColumnAsync(db, "UserProfiles", "PersonaRole", "TEXT", nullable: false, defaultSql: "'Business Owner'");
                await EnsureColumnAsync(db, "UserProfiles", "PrimaryGoal", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "UserProfiles", "CreatedAtUtc", "TEXT", nullable: true);
                await EnsureColumnAsync(db, "UserProfiles", "UpdatedAtUtc", "TEXT", nullable: true);
            }

            await db.Database.ExecuteSqlRawAsync(@"
CREATE UNIQUE INDEX IF NOT EXISTS IX_UserProfiles_UserId
ON UserProfiles (UserId);");
        }


        private static async Task NormalizeSourceBooksAsync(ApplicationDbContext db)
        {
            // Remove legacy book-title labels from existing data so UI no longer shows them.
            // Safe to run repeatedly.
            await db.Database.ExecuteSqlRawAsync(@"
UPDATE Strategies
SET SourceBook = NULL
WHERE SourceBook IS NOT NULL
  AND (SourceBook LIKE '%33 Strategies of War%' OR SourceBook LIKE '%Atomic Habits%');
");
        }


        // Optional for later (migrations-based)
        public static void EnsureDatabaseUpToDate(WebApplication app)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.Migrate();
        }
    }
}
