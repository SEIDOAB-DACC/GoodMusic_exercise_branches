USE [sql-music];
GO

--create a schemas
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'gstusr')
    EXEC('CREATE SCHEMA gstusr');
GO
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'usr')
    EXEC('CREATE SCHEMA usr');
GO

--create a view that gives overview of the database content
CREATE OR ALTER VIEW gstusr.vwInfoDb AS
    SELECT (SELECT COUNT(*) FROM supusr.MusicGroups WHERE Seeded = 1) as nrSeededMusicGroups, 
        (SELECT COUNT(*) FROM supusr.MusicGroups WHERE Seeded = 0) as nrUnseededMusicGroups,
        (SELECT COUNT(*) FROM supusr.Albums WHERE Seeded = 1) as nrSeededAlbums, 
        (SELECT COUNT(*) FROM supusr.Albums WHERE Seeded = 0) as nrUnseededAlbums;
GO

--create the DeleteAll procedure
CREATE OR ALTER PROC supusr.spDeleteAll
    @seededParam BIT = 1,
    @nrMusicGroupsAffected INT OUTPUT,
    @nrAlbumsAffected INT OUTPUT
AS

    SET NOCOUNT ON;

    SELECT  @nrMusicGroupsAffected = COUNT(*) FROM supusr.MusicGroups WHERE Seeded = @seededParam;
    SELECT  @nrAlbumsAffected = COUNT(*) FROM supusr.Albums WHERE Seeded = @seededParam;

    DELETE FROM supusr.MusicGroups WHERE Seeded = @seededParam;
    DELETE FROM supusr.Albums WHERE Seeded = @seededParam;

    --throw our own error
    --;THROW 999999, 'Error occurred in supusr.spDeleteAll', 1

    SELECT * FROM gstusr.vwInfoDb;
GO




