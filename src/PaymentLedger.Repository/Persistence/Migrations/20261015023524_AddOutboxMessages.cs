using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentLedger.Repository.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_on_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    processed_on_utc = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true),
                    retry_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_occurred_on_utc",
                table: "outbox_messages",
                column: "occurred_on_utc");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_processed_on_utc",
                table: "outbox_messages",
                column: "processed_on_utc",
                filter: "\"processed_on_utc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages");
        }
    }
}
