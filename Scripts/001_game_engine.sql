-- Boardwalk game engine tables (PostgreSQL).
--
-- PREFERRED: from the API project folder run
--     dotnet ef migrations add AddGameEngine
--     dotnet ef database update
-- which generates the same schema from the models in Data/Models/Game.cs and AppDbContext.
--
-- Use THIS script only if you want to apply the schema by hand. Do not do both:
-- if you run this script, also keep EF in sync afterwards (add the migration, then mark it applied).

BEGIN;

CREATE TABLE "Games" (
    "Id"                        uuid                     NOT NULL,
    "LobbyId"                   uuid                     NOT NULL,
    "Status"                    integer                  NOT NULL,   -- 0 InProgress, 1 Finished
    "CurrentTurnUserId"         uuid                     NOT NULL,
    "TurnNumber"                integer                  NOT NULL,
    "HasRolled"                 boolean                  NOT NULL,
    "LastDie1"                  integer                  NOT NULL,
    "LastDie2"                  integer                  NOT NULL,
    "DoublesCount"              integer                  NOT NULL,
    "PendingPurchaseTileIndex"  integer                  NULL,
    "PendingDebtAmount"         integer                  NOT NULL,
    "PendingDebtCreditorUserId" uuid                     NULL,
    "ChanceDeckOrder"           character varying(200)   NOT NULL,
    "ChanceDeckPosition"        integer                  NOT NULL,
    "LastChanceCardId"          integer                  NULL,
    "WinnerUserId"              uuid                     NULL,
    "EventCounter"              integer                  NOT NULL,
    "CreatedAt"                 timestamp with time zone NOT NULL,
    "FinishedAt"                timestamp with time zone NULL,
    CONSTRAINT "PK_Games" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Games_Lobbies_LobbyId" FOREIGN KEY ("LobbyId") REFERENCES "Lobbies" ("Id") ON DELETE RESTRICT
);
CREATE INDEX "IX_Games_LobbyId" ON "Games" ("LobbyId");

CREATE TABLE "GamePlayers" (
    "Id"                uuid    NOT NULL,
    "GameId"            uuid    NOT NULL,
    "UserId"            uuid    NOT NULL,
    "TurnOrder"         integer NOT NULL,
    "Cash"              integer NOT NULL,
    "Position"          integer NOT NULL,
    "InJail"            boolean NOT NULL,
    "JailTurns"         integer NOT NULL,
    "GetOutOfJailCards" integer NOT NULL,
    "IsBankrupt"        boolean NOT NULL,
    CONSTRAINT "PK_GamePlayers" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_GamePlayers_Games_GameId" FOREIGN KEY ("GameId") REFERENCES "Games" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_GamePlayers_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
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

CREATE TABLE "GameEvents" (
    "Id"        uuid                     NOT NULL,
    "GameId"    uuid                     NOT NULL,
    "Sequence"  integer                  NOT NULL,
    "Message"   character varying(500)   NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_GameEvents" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_GameEvents_Games_GameId" FOREIGN KEY ("GameId") REFERENCES "Games" ("Id") ON DELETE CASCADE
);
CREATE INDEX "IX_GameEvents_GameId_Sequence" ON "GameEvents" ("GameId", "Sequence");

COMMIT;

-- Handy queries while testing:
--   SELECT "Id","Status","CurrentTurnUserId","TurnNumber","HasRolled","PendingPurchaseTileIndex","PendingDebtAmount" FROM "Games";
--   SELECT u."FirstName", p."Cash", p."Position", p."InJail", p."IsBankrupt" FROM "GamePlayers" p JOIN "Users" u ON u."Id" = p."UserId" ORDER BY p."TurnOrder";
--   SELECT "TileIndex","OwnerUserId","Houses","IsMortgaged" FROM "GameProperties" WHERE "OwnerUserId" IS NOT NULL;
--   SELECT "Sequence","Message" FROM "GameEvents" ORDER BY "Sequence" DESC LIMIT 20;
