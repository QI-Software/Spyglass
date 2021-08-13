using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Spyglass.Database.Migrations
{
    public partial class Tags : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "Moderation.Tags_ID_seq",
                startValue: 0L,
                minValue: 0L);

            migrationBuilder.CreateTable(
                name: "Utilities.Tags",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('\"Moderation.Tags_ID_seq\"'::regclass)"),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Uses = table.Column<long>(type: "bigint", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    LastUpdatedBy = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    LastUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Utilities.Tags", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Utilities.Tags");

            migrationBuilder.DropSequence(
                name: "Moderation.Tags_ID_seq");
        }
    }
}
