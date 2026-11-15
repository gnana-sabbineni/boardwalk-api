-- Boardwalk: Monopoly game module + computer players (PostgreSQL).
--
-- PREFERRED: generate the schema with EF from the API project folder (BoardWalk.Api):
--     dotnet ef migrations add AddMonopolyModule
--     dotnet ef database update
-- This script is the same schema written by hand, for when you want to read or apply it manually.
-- Do not do both. (The six bot users are NOT inserted here: the API creates them itself on startup.)

BEGIN;

-- Computer players are ordinary rows in Users, flagged.
ALTER TABLE "Users" ADD COLUMN "IsBot" boolean NOT NULL DEFAULT FALSE;

-- NOTE: UserId / RoomId columns below deliberately have NO foreign keys: the game module does not depend on
-- the host's user or lobby tables.

CREATE TABLE "Games" (
    "Id"                        uuid                     NOT NULL,
    "RoomId"                    uuid                     NOT NULL,   -- the lobby id
    "Status"                    integer                  NOT NULL,   -- 0 InProgress, 1 Finished, 2 Abandoned
    "CurrentTurnUserId"         uuid                     NOT NULL,
    "TurnNumber"                integer                  NOT NULL,
    "HasRolled"                 boolean                  NOT NULL,
    "LastDie1"                  integer                  NOT NULL,
    "LastDie2"                  integer                  NOT NULL,
    "DoublesCount"              integer                  NOT NULL,
    "DiceSeed"                  integer                  NOT NULL,   -- server-side only: reveals future rolls
    "RollCounter"               integer                  NOT NULL,
    "PendingPurchaseTileIndex"  integer                  NULL,
    "PendingDebtAmount"         integer                  NOT NULL,
    "PendingDebtCreditorUserId" uuid                     NULL,
    "ChanceDeckOrder"           character varying(200)   NOT NULL,
    "ChanceDeckPosition"        integer                  NOT NULL,
    "LastChanceCardId"          integer                  NULL,
    "WinnerUserId"              uuid                     NULL,
    "TurnDeadlineAt"            timestamp with time zone NULL,
    "EventCounter"              integer                  NOT NULL,
    "CreatedAt"                 timestamp with time zone NOT NULL,
    "FinishedAt"                timestamp with time zone NULL,
    CONSTRAINT "PK_Games" PRIMARY KEY ("Id")
);
CREATE INDEX "IX_Games_RoomId" ON "Games" ("RoomId");
CREATE INDEX "IX_Games_Status_TurnDeadlineAt" ON "Games" ("Status", "TurnDeadlineAt");   -- the turn timer's scan

CREATE TABLE "GamePlayers" (
    "Id"                uuid                   NOT NULL,
    "GameId"            uuid                   NOT NULL,
    "UserId"            uuid                   NOT NULL,
    "FirstName"         character varying(50)  NOT NULL,
    "LastName"          character varying(50)  NOT NULL,
    "TurnOrder"         integer                NOT NULL,   -- the "seat" used in event args
    "ControlledBy"      integer                NOT NULL,   -- 0 Human, 1 Bot
    "MissedTurns"       integer                NOT NULL,
    "Cash"              integer                NOT NULL,
    "Position"          integer                NOT NULL,
    "InJail"            boolean                NOT NULL,
    "JailTurns"         integer                NOT NULL,
    "GetOutOfJailCards" integer                NOT NULL,
    "IsBankrupt"        boolean                NOT NULL,
    CONSTRAINT "PK_GamePlayers" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_GamePlayers_Games_GameId" FOREIGN KEY ("GameId") REFERENCES "Games" ("Id") ON DELETE CASCADE
);
CREATE UNIQUE INDEX "IX_GamePlayers_GameId_UserId" ON "GamePlayers" ("GameId", "UserId");
CREATE INDEX "IX_GamePlayers_UserId" ON "GamePlayers" ("UserId");

CREATE TABLE "GameProperties" (
    "Id"          uuid    NOT NULL,
    "GameId"      uuid    NOT NULL,
    "TileIndex"   integer NOT NULL,
    "OwnerUserId" uuid    NULL,
    "Houses"      integer NOT NULL,   -- 0-4 houses, 5 = hotel
    "IsMortgaged" boolean NOT NULL,
    CONSTRAINT "PK_GameProperties" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_GameProperties_Games_GameId" FOREIGN KEY ("GameId") REFERENCES "Games" ("Id") ON DELETE CASCADE
);
CREATE UNIQUE INDEX "IX_GameProperties_GameId_TileIndex" ON "GameProperties" ("GameId", "TileIndex");

-- The activity log: one row per event, args as a positional integer array (meaning defined per type in GameEventSchema).
CREATE TABLE "GameEvents" (
    "Id"       uuid      NOT NULL,
    "GameId"   uuid      NOT NULL,
    "Sequence" integer   NOT NULL,
    "Turn"     integer   NOT NULL,
    "Type"     integer   NOT NULL,
    "Args"     integer[] NOT NULL,
    CONSTRAINT "PK_GameEvents" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_GameEvents_Games_GameId" FOREIGN KEY ("GameId") REFERENCES "Games" ("Id") ON DELETE CASCADE
);
CREATE UNIQUE INDEX "IX_GameEvents_GameId_Sequence" ON "GameEvents" ("GameId", "Sequence");

-- One row per player per turn (turn 0 = game start): the "summary at any point".
CREATE TABLE "GameSnapshots" (
    "Id"            uuid    NOT NULL,
    "GameId"        uuid    NOT NULL,
    "Turn"          integer NOT NULL,
    "Seat"          integer NOT NULL,
    "EventSequence" integer NOT NULL,   -- activity-log position at that moment
    "Die1"          integer NOT NULL,
    "Die2"          integer NOT NULL,
    "RollCounter"   integer NOT NULL,
    "Cash"          integer NOT NULL,
    "NetWorth"      integer NOT NULL,
    "Position"      integer NOT NULL,
    "Properties"    integer NOT NULL,
    "Houses"        integer NOT NULL,
    "Hotels"        integer NOT NULL,
    "Mortgaged"     integer NOT NULL,
    "InJail"        boolean NOT NULL,
    "IsBankrupt"    boolean NOT NULL,
    "ControlledBy"  integer NOT NULL,
    CONSTRAINT "PK_GameSnapshots" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_GameSnapshots_Games_GameId" FOREIGN KEY ("GameId") REFERENCES "Games" ("Id") ON DELETE CASCADE
);
CREATE UNIQUE INDEX "IX_GameSnapshots_GameId_Turn_Seat" ON "GameSnapshots" ("GameId", "Turn", "Seat");

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

-- Handy while testing:
--   SELECT "Id","Status","TurnNumber","TurnDeadlineAt" FROM "Games";
--   SELECT "Sequence","Turn","Type","Args" FROM "GameEvents" WHERE "GameId" = '<id>' ORDER BY "Sequence";
--   SELECT "Turn","Seat","Cash","NetWorth","Properties" FROM "GameSnapshots" WHERE "GameId" = '<id>' ORDER BY "Turn","Seat";
--   SELECT "FirstName","ControlledBy","MissedTurns","Cash" FROM "GamePlayers" WHERE "GameId" = '<id>' ORDER BY "TurnOrder";
