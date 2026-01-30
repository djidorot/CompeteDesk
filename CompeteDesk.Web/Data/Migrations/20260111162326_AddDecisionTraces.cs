using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompeteDesk.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDecisionTraces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Actions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WorkspaceId = table.Column<int>(type: "INTEGER", nullable: true),
                    StrategyId = table.Column<int>(type: "INTEGER", nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SourceBook = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Actions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BusinessAnalysisReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WorkspaceId = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    BusinessType = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Country = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    AiInsightsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessAnalysisReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DecisionTraces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<int>(type: "INTEGER", nullable: true),
                    Feature = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    EntityId = table.Column<int>(type: "INTEGER", nullable: true),
                    EntityTitle = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    InputJson = table.Column<string>(type: "TEXT", nullable: true),
                    OutputJson = table.Column<string>(type: "TEXT", nullable: true),
                    AiProvider = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Model = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    Temperature = table.Column<double>(type: "REAL", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecisionTraces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WarIntel",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WorkspaceId = table.Column<int>(type: "INTEGER", nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Signal = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Confidence = table.Column<int>(type: "INTEGER", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    ObservedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarIntel", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WarPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WorkspaceId = table.Column<int>(type: "INTEGER", nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Objective = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Approach = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Assumptions = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Risks = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Contingencies = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    StartAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SourceBook = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebsiteAnalysisReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    FinalUrl = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    HttpStatusCode = table.Column<int>(type: "INTEGER", nullable: false),
                    ResponseTimeMs = table.Column<long>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    MetaDescription = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    WordCount = table.Column<int>(type: "INTEGER", nullable: false),
                    H1Count = table.Column<int>(type: "INTEGER", nullable: false),
                    H2Count = table.Column<int>(type: "INTEGER", nullable: false),
                    InternalLinkCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ExternalLinkCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ImageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ImagesMissingAltCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IsHttps = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasViewportMeta = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasRobotsNoindex = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasCanonicalLink = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasOpenGraph = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasCspHeader = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasHstsHeader = table.Column<bool>(type: "INTEGER", nullable: false),
                    AiInsightsJson = table.Column<string>(type: "TEXT", nullable: true),
                    AiSummary = table.Column<string>(type: "TEXT", nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    WorkspaceId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebsiteAnalysisReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workspaces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    OwnerUserId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    BusinessType = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Country = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    BusinessProfileUpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Strategies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    WorkspaceId = table.Column<int>(type: "INTEGER", nullable: true),
                    OwnerId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    SourceBook = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    CorePrinciple = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 24, nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AiInsightsJson = table.Column<string>(type: "TEXT", nullable: true),
                    AiSummary = table.Column<string>(type: "TEXT", nullable: true),
                    AiUpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Strategies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Strategies_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Actions_OwnerId_Status",
                table: "Actions",
                columns: new[] { "OwnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Actions_StrategyId_OwnerId",
                table: "Actions",
                columns: new[] { "StrategyId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Actions_WorkspaceId_OwnerId",
                table: "Actions",
                columns: new[] { "WorkspaceId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAnalysisReports_OwnerId_CreatedAtUtc",
                table: "BusinessAnalysisReports",
                columns: new[] { "OwnerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessAnalysisReports_WorkspaceId",
                table: "BusinessAnalysisReports",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_DecisionTraces_OwnerId_CreatedAtUtc",
                table: "DecisionTraces",
                columns: new[] { "OwnerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DecisionTraces_OwnerId_Feature",
                table: "DecisionTraces",
                columns: new[] { "OwnerId", "Feature" });

            migrationBuilder.CreateIndex(
                name: "IX_DecisionTraces_WorkspaceId_OwnerId",
                table: "DecisionTraces",
                columns: new[] { "WorkspaceId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Strategies_OwnerId_Status",
                table: "Strategies",
                columns: new[] { "OwnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Strategies_WorkspaceId_OwnerId",
                table: "Strategies",
                columns: new[] { "WorkspaceId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_WarIntel_OwnerId_Confidence",
                table: "WarIntel",
                columns: new[] { "OwnerId", "Confidence" });

            migrationBuilder.CreateIndex(
                name: "IX_WarIntel_WorkspaceId_OwnerId",
                table: "WarIntel",
                columns: new[] { "WorkspaceId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_WarPlans_OwnerId_Status",
                table: "WarPlans",
                columns: new[] { "OwnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WarPlans_WorkspaceId_OwnerId",
                table: "WarPlans",
                columns: new[] { "WorkspaceId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_WebsiteAnalysisReports_OwnerId_CreatedAtUtc",
                table: "WebsiteAnalysisReports",
                columns: new[] { "OwnerId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WebsiteAnalysisReports_OwnerId_Url",
                table: "WebsiteAnalysisReports",
                columns: new[] { "OwnerId", "Url" });

            migrationBuilder.CreateIndex(
                name: "IX_WebsiteAnalysisReports_WorkspaceId",
                table: "WebsiteAnalysisReports",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_OwnerUserId_Name",
                table: "Workspaces",
                columns: new[] { "OwnerUserId", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Actions");

            migrationBuilder.DropTable(
                name: "BusinessAnalysisReports");

            migrationBuilder.DropTable(
                name: "DecisionTraces");

            migrationBuilder.DropTable(
                name: "Strategies");

            migrationBuilder.DropTable(
                name: "WarIntel");

            migrationBuilder.DropTable(
                name: "WarPlans");

            migrationBuilder.DropTable(
                name: "WebsiteAnalysisReports");

            migrationBuilder.DropTable(
                name: "Workspaces");
        }
    }
}
