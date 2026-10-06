BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006173252_ParametroExigePrincipal'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Clave', N'CreadoPorId', N'Descripcion', N'FechaCreacion', N'FechaModificacion', N'ModificadoPorId', N'TipoDato', N'Valor') AND [object_id] = OBJECT_ID(N'[dbo].[Parametro]'))
        SET IDENTITY_INSERT [dbo].[Parametro] ON;
    EXEC(N'INSERT INTO [dbo].[Parametro] ([Id], [Clave], [CreadoPorId], [Descripcion], [FechaCreacion], [FechaModificacion], [ModificadoPorId], [TipoDato], [Valor])
    VALUES (10, ''PROYECTO_EXIGE_PRINCIPAL'', 1, N''1 = creación, actualización de personal y reactivación exigen al menos 1 principal (C10); 0 = basta 1 persona (principal o back)'', ''2026-09-29T00:00:00Z'', NULL, NULL, ''BOOL'', N''0'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Clave', N'CreadoPorId', N'Descripcion', N'FechaCreacion', N'FechaModificacion', N'ModificadoPorId', N'TipoDato', N'Valor') AND [object_id] = OBJECT_ID(N'[dbo].[Parametro]'))
        SET IDENTITY_INSERT [dbo].[Parametro] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006173252_ParametroExigePrincipal'
)
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006173252_ParametroExigePrincipal', N'10.0.12');
END;

COMMIT;
GO

