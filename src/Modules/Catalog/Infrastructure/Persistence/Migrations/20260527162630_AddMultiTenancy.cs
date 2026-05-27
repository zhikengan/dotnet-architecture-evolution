using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "catalog",
                table: "products",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "catalog",
                table: "outbox_messages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_products_tenant_id",
                schema: "catalog",
                table: "products",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_tenant_id",
                schema: "catalog",
                table: "outbox_messages",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_products_tenant_id",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_outbox_messages_tenant_id",
                schema: "catalog",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "catalog",
                table: "outbox_messages");
        }
    }
}
