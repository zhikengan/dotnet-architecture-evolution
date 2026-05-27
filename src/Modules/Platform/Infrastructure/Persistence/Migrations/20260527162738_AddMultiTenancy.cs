using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_feature_flags",
                schema: "platform",
                table: "feature_flags");

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "platform",
                table: "feature_flags",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_feature_flags",
                schema: "platform",
                table: "feature_flags",
                columns: new[] { "tenant_id", "name" });

            migrationBuilder.CreateTable(
                name: "tenants",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tenants_slug",
                schema: "platform",
                table: "tenants",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenants",
                schema: "platform");

            migrationBuilder.DropPrimaryKey(
                name: "PK_feature_flags",
                schema: "platform",
                table: "feature_flags");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "platform",
                table: "feature_flags");

            migrationBuilder.AddPrimaryKey(
                name: "PK_feature_flags",
                schema: "platform",
                table: "feature_flags",
                column: "name");
        }
    }
}
