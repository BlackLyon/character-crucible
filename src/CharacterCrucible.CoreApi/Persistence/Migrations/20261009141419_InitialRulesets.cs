using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CharacterCrucible.CoreApi.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialRulesets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "rulesets");

            migrationBuilder.CreateTable(
                name: "ability_definition",
                schema: "rulesets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ruleset_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    ability_type = table.Column<int>(type: "integer", nullable: false),
                    requires_approval = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_available = table.Column<bool>(type: "boolean", nullable: false),
                    cost_rule_factor = table.Column<int>(type: "integer", nullable: false),
                    cost_rule_op = table.Column<int>(type: "integer", nullable: false),
                    cost_rule_operand = table.Column<int>(type: "integer", nullable: false),
                    prerequisites = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ability_definition", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "archetype_definition",
                schema: "rulesets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ruleset_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_available = table.Column<bool>(type: "boolean", nullable: false),
                    cap_modifiers = table.Column<string>(type: "jsonb", nullable: false),
                    cost_modifiers = table.Column<string>(type: "jsonb", nullable: false),
                    granted_ranks = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_archetype_definition", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "domain_definition",
                schema: "rulesets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ruleset_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    bands = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_domain_definition", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ruleset",
                schema: "rulesets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    publisher = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    derived_from = table.Column<Guid>(type: "uuid", nullable: true),
                    current_version_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ruleset", x => x.id);
                    table.ForeignKey(
                        name: "fk_ruleset_ruleset_derived_from",
                        column: x => x.derived_from,
                        principalSchema: "rulesets",
                        principalTable: "ruleset",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ruleset_version",
                schema: "rulesets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ruleset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    major_version = table.Column<int>(type: "integer", nullable: false),
                    minor_version = table.Column<int>(type: "integer", nullable: false),
                    release_type = table.Column<int>(type: "integer", nullable: false),
                    release_notes = table.Column<string>(type: "text", nullable: true),
                    publication_content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    publication_published_by = table.Column<Guid>(type: "uuid", nullable: true),
                    publication_published_content = table.Column<string>(type: "text", nullable: true),
                    publication_published_date = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    publication_publisher = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    publication_ruleset_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    publication_schema_version = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ruleset_version", x => x.id);
                    table.CheckConstraint("ck_ruleset_version_publication_all_or_nothing", "num_nulls(publication_ruleset_name, publication_publisher, publication_published_date, publication_published_by, publication_published_content, publication_content_hash, publication_schema_version) IN (0, 7)");
                    table.ForeignKey(
                        name: "fk_ruleset_version_ruleset_ruleset_id",
                        column: x => x.ruleset_id,
                        principalSchema: "rulesets",
                        principalTable: "ruleset",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "trait_definition",
                schema: "rulesets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ruleset_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    domain_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    min_value = table.Column<int>(type: "integer", nullable: false),
                    max_value = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_available = table.Column<bool>(type: "boolean", nullable: false),
                    cost_rule_factor = table.Column<int>(type: "integer", nullable: false),
                    cost_rule_op = table.Column<int>(type: "integer", nullable: false),
                    cost_rule_operand = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_trait_definition", x => x.id);
                    table.ForeignKey(
                        name: "fk_trait_definition_ruleset_versions_ruleset_version_id",
                        column: x => x.ruleset_version_id,
                        principalSchema: "rulesets",
                        principalTable: "ruleset_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ability_definition_ruleset_version_id_key",
                schema: "rulesets",
                table: "ability_definition",
                columns: new[] { "ruleset_version_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_archetype_definition_ruleset_version_id_key",
                schema: "rulesets",
                table: "archetype_definition",
                columns: new[] { "ruleset_version_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_domain_definition_ruleset_version_id_key",
                schema: "rulesets",
                table: "domain_definition",
                columns: new[] { "ruleset_version_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ruleset_current_version_id",
                schema: "rulesets",
                table: "ruleset",
                column: "current_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_ruleset_derived_from",
                schema: "rulesets",
                table: "ruleset",
                column: "derived_from");

            migrationBuilder.CreateIndex(
                name: "ix_ruleset_version_ruleset_id_major_minor",
                schema: "rulesets",
                table: "ruleset_version",
                columns: new[] { "ruleset_id", "major_version", "minor_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_ruleset_version_one_draft_per_ruleset",
                schema: "rulesets",
                table: "ruleset_version",
                column: "ruleset_id",
                unique: true,
                filter: "publication_schema_version IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_trait_definition_ruleset_version_id_key",
                schema: "rulesets",
                table: "trait_definition",
                columns: new[] { "ruleset_version_id", "key" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_ability_definition_ruleset_versions_ruleset_version_id",
                schema: "rulesets",
                table: "ability_definition",
                column: "ruleset_version_id",
                principalSchema: "rulesets",
                principalTable: "ruleset_version",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_archetype_definition_ruleset_versions_ruleset_version_id",
                schema: "rulesets",
                table: "archetype_definition",
                column: "ruleset_version_id",
                principalSchema: "rulesets",
                principalTable: "ruleset_version",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_domain_definition_ruleset_versions_ruleset_version_id",
                schema: "rulesets",
                table: "domain_definition",
                column: "ruleset_version_id",
                principalSchema: "rulesets",
                principalTable: "ruleset_version",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_ruleset_ruleset_versions_current_version_id",
                schema: "rulesets",
                table: "ruleset",
                column: "current_version_id",
                principalSchema: "rulesets",
                principalTable: "ruleset_version",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_ruleset_ruleset_versions_current_version_id",
                schema: "rulesets",
                table: "ruleset");

            migrationBuilder.DropTable(
                name: "ability_definition",
                schema: "rulesets");

            migrationBuilder.DropTable(
                name: "archetype_definition",
                schema: "rulesets");

            migrationBuilder.DropTable(
                name: "domain_definition",
                schema: "rulesets");

            migrationBuilder.DropTable(
                name: "trait_definition",
                schema: "rulesets");

            migrationBuilder.DropTable(
                name: "ruleset_version",
                schema: "rulesets");

            migrationBuilder.DropTable(
                name: "ruleset",
                schema: "rulesets");
        }
    }
}
