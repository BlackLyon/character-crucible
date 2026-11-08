using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CharacterCrucible.CoreApi.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "characters");

            migrationBuilder.CreateTable(
                name: "character",
                schema: "characters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ruleset_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    archetype_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sex = table.Column<int>(type: "integer", nullable: false),
                    race = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    age = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    total_earned_xp = table.Column<int>(type: "integer", nullable: false),
                    unspent_xp = table.Column<int>(type: "integer", nullable: false),
                    created_date = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    last_request_submitted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    abilities = table.Column<string>(type: "jsonb", nullable: false),
                    traits = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_character", x => x.id);
                    table.ForeignKey(
                        name: "fk_character_ruleset_version_ruleset_version_id",
                        column: x => x.ruleset_version_id,
                        principalSchema: "rulesets",
                        principalTable: "ruleset_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "advancement_request",
                schema: "characters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_date = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    target_kind = table.Column<int>(type: "integer", nullable: false),
                    target_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    target_rating = table.Column<int>(type: "integer", nullable: true),
                    quoted_cost = table.Column<int>(type: "integer", nullable: false),
                    evaluated_ruleset_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evaluated_engine_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    request_status = table.Column<int>(type: "integer", nullable: false),
                    resolved_date = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    resolution_reason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_date = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_advancement_request", x => x.id);
                    table.ForeignKey(
                        name: "fk_advancement_request_character_character_id",
                        column: x => x.character_id,
                        principalSchema: "characters",
                        principalTable: "character",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_advancement_request_evaluated_ruleset_version",
                        column: x => x.evaluated_ruleset_version_id,
                        principalSchema: "rulesets",
                        principalTable: "ruleset_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_entry",
                schema: "characters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_role = table.Column<int>(type: "integer", nullable: false),
                    action = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    advancement_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ruleset_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    engine_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    change = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_entry", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_entry_advancement_request_advancement_request_id",
                        column: x => x.advancement_request_id,
                        principalSchema: "characters",
                        principalTable: "advancement_request",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_audit_entry_character_character_id",
                        column: x => x.character_id,
                        principalSchema: "characters",
                        principalTable: "character",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_audit_entry_ruleset_version_ruleset_version_id",
                        column: x => x.ruleset_version_id,
                        principalSchema: "rulesets",
                        principalTable: "ruleset_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "xp_ledger_entry",
                schema: "characters",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    character_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delta = table.Column<int>(type: "integer", nullable: false),
                    total_earned_xp_after = table.Column<int>(type: "integer", nullable: false),
                    unspent_xp_after = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    advancement_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_xp_ledger_entry", x => x.id);
                    table.ForeignKey(
                        name: "fk_xp_ledger_entry_advancement_request_advancement_request_id",
                        column: x => x.advancement_request_id,
                        principalSchema: "characters",
                        principalTable: "advancement_request",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_xp_ledger_entry_character_character_id",
                        column: x => x.character_id,
                        principalSchema: "characters",
                        principalTable: "character",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_advancement_request_character_id",
                schema: "characters",
                table: "advancement_request",
                column: "character_id");

            migrationBuilder.CreateIndex(
                name: "ix_advancement_request_evaluated_ruleset_version_id",
                schema: "characters",
                table: "advancement_request",
                column: "evaluated_ruleset_version_id");

            migrationBuilder.CreateIndex(
                name: "ux_advancement_request_one_in_flight_per_item",
                schema: "characters",
                table: "advancement_request",
                columns: new[] { "character_id", "target_kind", "target_key" },
                unique: true,
                filter: "request_status IN (3, 5, 6)");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_advancement_request_id",
                schema: "characters",
                table: "audit_entry",
                column: "advancement_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_character_id_occurred_at",
                schema: "characters",
                table: "audit_entry",
                columns: new[] { "character_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_ruleset_version_id",
                schema: "characters",
                table: "audit_entry",
                column: "ruleset_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_character_ruleset_version_id",
                schema: "characters",
                table: "character",
                column: "ruleset_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_xp_ledger_entry_advancement_request_id",
                schema: "characters",
                table: "xp_ledger_entry",
                column: "advancement_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_xp_ledger_entry_character_id_occurred_at",
                schema: "characters",
                table: "xp_ledger_entry",
                columns: new[] { "character_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_entry",
                schema: "characters");

            migrationBuilder.DropTable(
                name: "xp_ledger_entry",
                schema: "characters");

            migrationBuilder.DropTable(
                name: "advancement_request",
                schema: "characters");

            migrationBuilder.DropTable(
                name: "character",
                schema: "characters");
        }
    }
}
