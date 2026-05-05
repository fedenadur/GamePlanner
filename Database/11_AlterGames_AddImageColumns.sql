ALTER TABLE Games
    ADD CoverImageUrl    NVARCHAR(500) NULL,
        LastImageSyncDate DATETIME     NULL,
        IGDBUpdatedAt    DATETIME      NULL;
GO

CREATE OR ALTER PROCEDURE sp_UpdateGameCover
    @Id               INT,
    @CoverImageUrl    NVARCHAR(500),
    @IGDBUpdatedAt    DATETIME
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Games
    SET CoverImageUrl     = @CoverImageUrl,
        LastImageSyncDate = GETUTCDATE(),
        IGDBUpdatedAt     = @IGDBUpdatedAt
    WHERE Id = @Id AND IsActive = 1;
END
