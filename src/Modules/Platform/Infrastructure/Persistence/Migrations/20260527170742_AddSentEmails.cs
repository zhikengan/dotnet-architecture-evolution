using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSentEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sent_emails",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    recipient = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    related_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sent_emails", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sent_emails_tenant_id",
                schema: "platform",
                table: "sent_emails",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sent_emails",
                schema: "platform");
        }
    }
}
