using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoardWalk.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLobbySystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentLobbyId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Users",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "Lobbies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HostUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MaxPlayers = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lobbies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lobbies_Users_HostUserId",
                        column: x => x.HostUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LobbyInvites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LobbyId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviteeUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LobbyInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LobbyInvites_Lobbies_LobbyId",
                        column: x => x.LobbyId,
                        principalTable: "Lobbies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LobbyInvites_Users_InviteeUserId",
                        column: x => x.InviteeUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LobbyInvites_Users_InviterUserId",
                        column: x => x.InviterUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LobbyMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LobbyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LobbyMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LobbyMembers_Lobbies_LobbyId",
                        column: x => x.LobbyId,
                        principalTable: "Lobbies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LobbyMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_CurrentLobbyId",
                table: "Users",
                column: "CurrentLobbyId");

            migrationBuilder.CreateIndex(
                name: "IX_Lobbies_HostUserId",
                table: "Lobbies",
                column: "HostUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LobbyInvites_InviteeUserId",
                table: "LobbyInvites",
                column: "InviteeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LobbyInvites_InviterUserId",
                table: "LobbyInvites",
                column: "InviterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LobbyInvites_LobbyId",
                table: "LobbyInvites",
                column: "LobbyId");

            migrationBuilder.CreateIndex(
                name: "IX_LobbyMembers_LobbyId",
                table: "LobbyMembers",
                column: "LobbyId");

            migrationBuilder.CreateIndex(
                name: "IX_LobbyMembers_UserId",
                table: "LobbyMembers",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Lobbies_CurrentLobbyId",
                table: "Users",
                column: "CurrentLobbyId",
                principalTable: "Lobbies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Lobbies_CurrentLobbyId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "LobbyInvites");

            migrationBuilder.DropTable(
                name: "LobbyMembers");

            migrationBuilder.DropTable(
                name: "Lobbies");

            migrationBuilder.DropIndex(
                name: "IX_Users_CurrentLobbyId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CurrentLobbyId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Users");
        }
    }
}
