-- Boardwalk trades (PostgreSQL). Apply AFTER 001_game_engine.sql.
--
-- PREFERRED: generate with EF instead:
--     dotnet ef migrations add AddPlayerActions
--     dotnet ef database update
-- Use this script only for a manual apply (do not do both).

BEGIN;

CREATE TABLE "Trades" (
    "Id"              uuid                     NOT NULL,
    "GameId"          uuid                     NOT NULL,
    "ProposerUserId"  uuid                     NOT NULL,
    "RecipientUserId" uuid                     NOT NULL,
    "OfferCash"       integer                  NOT NULL,
    "RequestCash"     integer                  NOT NULL,
    "Status"          integer                  NOT NULL,   -- 0 Pending, 1 Accepted, 2 Declined, 3 Cancelled
    "CreatedAt"       timestamp with time zone NOT NULL,
    "RespondedAt"     timestamp with time zone NULL,
    CONSTRAINT "PK_Trades" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Trades_Games_GameId" FOREIGN KEY ("GameId") REFERENCES "Games" ("Id") ON DELETE CASCADE
);
CREATE INDEX "IX_Trades_GameId_Status" ON "Trades" ("GameId", "Status");

CREATE TABLE "TradeItems" (
    "Id"           uuid    NOT NULL,
    "TradeId"      uuid    NOT NULL,
    "TileIndex"    integer NOT NULL,
    "FromProposer" boolean NOT NULL,
    CONSTRAINT "PK_TradeItems" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_TradeItems_Trades_TradeId" FOREIGN KEY ("TradeId") REFERENCES "Trades" ("Id") ON DELETE CASCADE
);
CREATE INDEX "IX_TradeItems_TradeId" ON "TradeItems" ("TradeId");

COMMIT;
