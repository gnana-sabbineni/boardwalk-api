using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardWalk.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMonopolyModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBot",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentTurnUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TurnNumber = table.Column<int>(type: "integer", nullable: false),
                    HasRolled = table.Column<bool>(type: "boolean", nullable: false),
                    LastDie1 = table.Column<int>(type: "integer", nullable: false),
                    LastDie2 = table.Column<int>(type: "integer", nullable: false),
                    DoublesCount = table.Column<int>(type: "integer", nullable: false),
                    DiceSeed = table.Column<int>(type: "integer", nullable: false),
                    RollCounter = table.Column<int>(type: "integer", nullable: false),
                    PendingPurchaseTileIndex = table.Column<int>(type: "integer", nullable: true),
                    PendingDebtAmount = table.Column<int>(type: "integer", nullable: false),
                    PendingDebtCreditorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChanceDeckOrder = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ChanceDeckPosition = table.Column<int>(type: "integer", nullable: false),
                    LastChanceCardId = table.Column<int>(type: "integer", nullable: true),
                    WinnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    TurnDeadlineAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EventCounter = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Turn = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Args = table.Column<int[]>(type: "integer[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameEvents_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GamePlayers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TurnOrder = table.Column<int>(type: "integer", nullable: false),
                    ControlledBy = table.Column<int>(type: "integer", nullable: false),
                    MissedTurns = table.Column<int>(type: "integer", nullable: false),
                    Cash = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    InJail = table.Column<bool>(type: "boolean", nullable: false),
                    JailTurns = table.Column<int>(type: "integer", nullable: false),
                    GetOutOfJailCards = table.Column<int>(type: "integer", nullable: false),
                    IsBankrupt = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GamePlayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GamePlayers_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameProperties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    TileIndex = table.Column<int>(type: "integer", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Houses = table.Column<int>(type: "integer", nullable: false),
                    IsMortgaged = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameProperties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameProperties_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GameSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Turn = table.Column<int>(type: "integer", nullable: false),
                    Seat = table.Column<int>(type: "integer", nullable: false),
                    EventSequence = table.Column<int>(type: "integer", nullable: false),
                    Die1 = table.Column<int>(type: "integer", nullable: false),
                    Die2 = table.Column<int>(type: "integer", nullable: false),
                    RollCounter = table.Column<int>(type: "integer", nullable: false),
                    Cash = table.Column<int>(type: "integer", nullable: false),
                    NetWorth = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Properties = table.Column<int>(type: "integer", nullable: false),
                    Houses = table.Column<int>(type: "integer", nullable: false),
                    Hotels = table.Column<int>(type: "integer", nullable: false),
                    Mortgaged = table.Column<int>(type: "integer", nullable: false),
                    InJail = table.Column<bool>(type: "boolean", nullable: false),
                    IsBankrupt = table.Column<bool>(type: "boolean", nullable: false),
                    ControlledBy = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameSnapshots_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Trades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferCash = table.Column<int>(type: "integer", nullable: false),
                    RequestCash = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trades_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TradeItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TradeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TileIndex = table.Column<int>(type: "integer", nullable: false),
                    FromProposer = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeItems_Trades_TradeId",
                        column: x => x.TradeId,
                        principalTable: "Trades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameEvents_GameId_Sequence",
                table: "GameEvents",
                columns: new[] { "GameId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GamePlayers_GameId_UserId",
                table: "GamePlayers",
                columns: new[] { "GameId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GamePlayers_UserId",
                table: "GamePlayers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GameProperties_GameId_TileIndex",
                table: "GameProperties",
                columns: new[] { "GameId", "TileIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_RoomId",
                table: "Games",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_Status_TurnDeadlineAt",
                table: "Games",
                columns: new[] { "Status", "TurnDeadlineAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GameSnapshots_GameId_Turn_Seat",
                table: "GameSnapshots",
                columns: new[] { "GameId", "Turn", "Seat" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradeItems_TradeId",
                table: "TradeItems",
                column: "TradeId");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_GameId_Status",
                table: "Trades",
                columns: new[] { "GameId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameEvents");

            migrationBuilder.DropTable(
                name: "GamePlayers");

            migrationBuilder.DropTable(
                name: "GameProperties");

            migrationBuilder.DropTable(
                name: "GameSnapshots");

            migrationBuilder.DropTable(
                name: "TradeItems");

            migrationBuilder.DropTable(
                name: "Trades");

            migrationBuilder.DropTable(
                name: "Games");

            migrationBuilder.DropColumn(
                name: "IsBot",
                table: "Users");
        }
    }
}
