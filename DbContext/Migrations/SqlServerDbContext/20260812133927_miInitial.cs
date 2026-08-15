using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DbContext.Migrations.SqlServerDbContext
{
    /// <inheritdoc />
    public partial class miInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "supusr");

            migrationBuilder.CreateTable(
                name: "MusicGroups",
                schema: "supusr",
                columns: table => new
                {
                    MusicGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "varchar(200)", nullable: true),
                    Genre = table.Column<int>(type: "int", nullable: false),
                    Seeded = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MusicGroups", x => x.MusicGroupId);
                });

            migrationBuilder.CreateTable(
                name: "Albums",
                schema: "supusr",
                columns: table => new
                {
                    AlbumId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "varchar(200)", nullable: false),
                    MusicGroupDbMMusicGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Seeded = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Albums", x => x.AlbumId);
                    table.ForeignKey(
                        name: "FK_Albums_MusicGroups_MusicGroupDbMMusicGroupId",
                        column: x => x.MusicGroupDbMMusicGroupId,
                        principalSchema: "supusr",
                        principalTable: "MusicGroups",
                        principalColumn: "MusicGroupId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Albums_MusicGroupDbMMusicGroupId",
                schema: "supusr",
                table: "Albums",
                column: "MusicGroupDbMMusicGroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Albums",
                schema: "supusr");

            migrationBuilder.DropTable(
                name: "MusicGroups",
                schema: "supusr");
        }
    }
}
