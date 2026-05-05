-- sp_EnrichGame: updates Genre and Tags for a game found via IGDB search
CREATE OR ALTER PROCEDURE sp_EnrichGame
    @Id    INT,
    @Genres NVARCHAR(500) = NULL,
    @Tags   NVARCHAR(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Games
    SET
        Genre = COALESCE(@Genres, Genre),
        Tags  = COALESCE(@Tags,   Tags)
    WHERE Id = @Id AND IsActive = 1;
END
