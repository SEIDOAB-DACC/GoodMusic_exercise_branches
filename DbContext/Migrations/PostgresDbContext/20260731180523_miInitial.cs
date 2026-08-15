using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DbContext.Migrations.PostgresDbContext
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
                name: "Addresses",
                schema: "supusr",
                columns: table => new
                {
                    AddressId = table.Column<Guid>(type: "uuid", nullable: false),
                    StreetAddress = table.Column<string>(type: "varchar(200)", nullable: false),
                    ZipCode = table.Column<int>(type: "integer", nullable: false),
                    City = table.Column<string>(type: "varchar(200)", nullable: false),
                    Country = table.Column<string>(type: "varchar(200)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Addresses", x => x.AddressId);
                });

            migrationBuilder.CreateTable(
                name: "Quotes",
                schema: "supusr",
                columns: table => new
                {
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteText = table.Column<string>(type: "varchar(200)", nullable: true),
                    Author = table.Column<string>(type: "varchar(200)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotes", x => x.QuoteId);
                });

            migrationBuilder.CreateTable(
                name: "Friends",
                schema: "supusr",
                columns: table => new
                {
                    FriendId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "varchar(200)", nullable: false),
                    AddressId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastName = table.Column<string>(type: "varchar(200)", nullable: true),
                    Email = table.Column<string>(type: "varchar(200)", nullable: true),
                    Birthday = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Friends", x => x.FriendId);
                    table.ForeignKey(
                        name: "FK_Friends_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalSchema: "supusr",
                        principalTable: "Addresses",
                        principalColumn: "AddressId");
                });

            migrationBuilder.CreateTable(
                name: "FriendDbMQuoteDbM",
                schema: "supusr",
                columns: table => new
                {
                    FriendsDbMFriendId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuotesDbMQuoteId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FriendDbMQuoteDbM", x => new { x.FriendsDbMFriendId, x.QuotesDbMQuoteId });
                    table.ForeignKey(
                        name: "FK_FriendDbMQuoteDbM_Friends_FriendsDbMFriendId",
                        column: x => x.FriendsDbMFriendId,
                        principalSchema: "supusr",
                        principalTable: "Friends",
                        principalColumn: "FriendId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FriendDbMQuoteDbM_Quotes_QuotesDbMQuoteId",
                        column: x => x.QuotesDbMQuoteId,
                        principalSchema: "supusr",
                        principalTable: "Quotes",
                        principalColumn: "QuoteId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Pets",
                schema: "supusr",
                columns: table => new
                {
                    PetId = table.Column<Guid>(type: "uuid", nullable: false),
                    FriendId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "varchar(200)", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Mood = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pets", x => x.PetId);
                    table.ForeignKey(
                        name: "FK_Pets_Friends_FriendId",
                        column: x => x.FriendId,
                        principalSchema: "supusr",
                        principalTable: "Friends",
                        principalColumn: "FriendId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Addresses_StreetAddress_ZipCode_City_Country",
                schema: "supusr",
                table: "Addresses",
                columns: new[] { "StreetAddress", "ZipCode", "City", "Country" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FriendDbMQuoteDbM_QuotesDbMQuoteId",
                schema: "supusr",
                table: "FriendDbMQuoteDbM",
                column: "QuotesDbMQuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_Friends_AddressId",
                schema: "supusr",
                table: "Friends",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Friends_FirstName_LastName",
                schema: "supusr",
                table: "Friends",
                columns: new[] { "FirstName", "LastName" });

            migrationBuilder.CreateIndex(
                name: "IX_Friends_LastName_FirstName",
                schema: "supusr",
                table: "Friends",
                columns: new[] { "LastName", "FirstName" });

            migrationBuilder.CreateIndex(
                name: "IX_Pets_FriendId",
                schema: "supusr",
                table: "Pets",
                column: "FriendId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FriendDbMQuoteDbM",
                schema: "supusr");

            migrationBuilder.DropTable(
                name: "Pets",
                schema: "supusr");

            migrationBuilder.DropTable(
                name: "Quotes",
                schema: "supusr");

            migrationBuilder.DropTable(
                name: "Friends",
                schema: "supusr");

            migrationBuilder.DropTable(
                name: "Addresses",
                schema: "supusr");
        }
    }
}
