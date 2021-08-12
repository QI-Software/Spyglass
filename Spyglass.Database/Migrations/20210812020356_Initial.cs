using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Spyglass.Database.Moderation;

namespace Spyglass.Database.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:BlacklistType", "mod_mail")
                .Annotation("Npgsql:Enum:InfractionType", "Note,Warn,Mute,Kick,Ban,Unmute,Unban,Undeafen");

            migrationBuilder.CreateSequence<int>(
                name: "Moderation.BlacklistedUsers_ID_seq",
                startValue: 0L,
                minValue: 0L);

            migrationBuilder.CreateSequence<int>(
                name: "Moderation.Infractions_ID_seq",
                startValue: 0L,
                minValue: 0L);

            migrationBuilder.CreateSequence<int>(
                name: "Moderation.Ongoing_ID_seq",
                startValue: 0L,
                minValue: 0L);

            migrationBuilder.CreateSequence<int>(
                name: "Moderation.ReactionRole_ID_seq",
                startValue: 0L,
                minValue: 0L);

            migrationBuilder.CreateTable(
                name: "Moderation.BlacklistedUsers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('\"Moderation.BlacklistedUsers_ID_seq\"'::regclass)"),
                    Type = table.Column<BlacklistType>(type: "\"BlacklistType\"", nullable: false),
                    StaffId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Moderation.BlacklistedUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Moderation.Infractions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('\"Moderation.Infractions_ID_seq\"'::regclass)"),
                    Type = table.Column<InfractionType>(type: "\"InfractionType\"", nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StaffId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    StaffName = table.Column<string>(type: "text", nullable: true),
                    UserName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Moderation.Infractions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Utilities.ReactionRole",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('\"Moderation.ReactionRole_ID_seq\"'::regclass)"),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    ReactionName = table.Column<string>(type: "text", nullable: true),
                    ReactionId = table.Column<decimal>(type: "numeric(20,0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Utilities.ReactionRole", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Moderation.Ongoing",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('\"Moderation.Ongoing_ID_seq\"'::regclass)"),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    EndTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Type = table.Column<InfractionType>(type: "\"InfractionType\"", nullable: false),
                    LinkedInfractionId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Moderation.Ongoing", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Moderation.Ongoing_Moderation.Infractions_LinkedInfractionId",
                        column: x => x.LinkedInfractionId,
                        principalTable: "Moderation.Infractions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Moderation.Ongoing_LinkedInfractionId",
                table: "Moderation.Ongoing",
                column: "LinkedInfractionId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Moderation.BlacklistedUsers");

            migrationBuilder.DropTable(
                name: "Moderation.Ongoing");

            migrationBuilder.DropTable(
                name: "Utilities.ReactionRole");

            migrationBuilder.DropTable(
                name: "Moderation.Infractions");

            migrationBuilder.DropSequence(
                name: "Moderation.BlacklistedUsers_ID_seq");

            migrationBuilder.DropSequence(
                name: "Moderation.Infractions_ID_seq");

            migrationBuilder.DropSequence(
                name: "Moderation.Ongoing_ID_seq");

            migrationBuilder.DropSequence(
                name: "Moderation.ReactionRole_ID_seq");
        }
    }
}
