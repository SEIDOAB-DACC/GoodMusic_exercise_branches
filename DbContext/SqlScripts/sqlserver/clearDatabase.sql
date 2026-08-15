USE [sql-music];
--GO

-- remove stored procedures
DROP PROCEDURE IF EXISTS supusr.spDeleteAll
GO

-- remove views
DROP VIEW IF EXISTS [gstusr].[vwInfoDb]
GO
    
-- Drop tables in the right order to avoid FK conflicts
DROP TABLE IF EXISTS supusr.ArtistDbMMusicGroupDbM;
DROP TABLE IF EXISTS supusr.Albums;
DROP TABLE IF EXISTS supusr.MusicGroups;
DROP TABLE IF EXISTS __EFMigrationsHistory;
GO