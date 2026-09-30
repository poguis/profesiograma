IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [dbo].[__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE TABLE [dbo].[CargoInfor] (
        [Id] int NOT NULL IDENTITY,
        [Cargo] nvarchar(200) NOT NULL,
        [CodigoInfor] varchar(20) NULL,
        [Activo] bit NOT NULL CONSTRAINT [DF_CargoInfor_Activo] DEFAULT CAST(1 AS bit),
        [CreadoPorId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_CargoInfor_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        [ModificadoPorId] int NULL,
        [FechaModificacion] datetime2(0) NULL,
        CONSTRAINT [PK_CargoInfor] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE TABLE [dbo].[Compania] (
        [Id] int NOT NULL,
        [Nombre] nvarchar(300) NOT NULL,
        [NombreComercial] nvarchar(300) NULL,
        [Ruc] varchar(13) NULL,
        [CreadoPorId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_Compania_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        [ModificadoPorId] int NULL,
        [FechaModificacion] datetime2(0) NULL,
        CONSTRAINT [PK_Compania] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE TABLE [dbo].[Departamento] (
        [Id] int NOT NULL IDENTITY,
        [Nombre] nvarchar(200) NOT NULL,
        [NombreCorto] nvarchar(60) NOT NULL,
        [Tipo] varchar(12) NOT NULL,
        [CodigoErp] varchar(10) NULL,
        [Orden] smallint NOT NULL CONSTRAINT [DF_Departamento_Orden] DEFAULT CAST(0 AS smallint),
        [Activo] bit NOT NULL CONSTRAINT [DF_Departamento_Activo] DEFAULT CAST(1 AS bit),
        [CreadoPorId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_Departamento_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        [ModificadoPorId] int NULL,
        [FechaModificacion] datetime2(0) NULL,
        CONSTRAINT [PK_Departamento] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Departamento_Tipo] CHECK ([Tipo] IN ('DEPARTAMENTO','UNIDAD'))
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE TABLE [dbo].[Empleado] (
        [Id] int NOT NULL IDENTITY,
        [CodigoEkon] varchar(20) NOT NULL,
        [Cedula] varchar(20) NULL,
        [NombreCompleto] nvarchar(200) NOT NULL,
        [Apellidos] nvarchar(120) NULL,
        [Nombres] nvarchar(120) NULL,
        [CorreoEmpresa] nvarchar(256) NULL,
        [CodEmpresa] varchar(10) NULL,
        [Empresa] nvarchar(200) NULL,
        [CodPuesto] varchar(10) NULL,
        [Puesto] nvarchar(200) NULL,
        [CodDepartamento] varchar(10) NULL,
        [Departamento] nvarchar(200) NULL,
        [CodUnidad] varchar(10) NULL,
        [Unidad] nvarchar(200) NULL,
        [CodArea] varchar(10) NULL,
        [Area] nvarchar(200) NULL,
        [CodSeccion] varchar(10) NULL,
        [Seccion] nvarchar(200) NULL,
        [FamiliaPuesto] nvarchar(100) NULL,
        [EstadoErp] char(1) NULL,
        [EsOrigenLegado] bit NOT NULL CONSTRAINT [DF_Empleado_EsOrigenLegado] DEFAULT CAST(0 AS bit),
        [FechaSincronizacion] datetime2(0) NULL,
        [CreadoPorId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_Empleado_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        [ModificadoPorId] int NULL,
        [FechaModificacion] datetime2(0) NULL,
        CONSTRAINT [PK_Empleado] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE TABLE [dbo].[Usuario] (
        [Id] int NOT NULL IDENTITY,
        [EntraObjectId] uniqueidentifier NULL,
        [Email] nvarchar(256) NOT NULL,
        [NombreMostrar] nvarchar(200) NOT NULL,
        [EmpleadoId] int NULL,
        [EsSistema] bit NOT NULL CONSTRAINT [DF_Usuario_EsSistema] DEFAULT CAST(0 AS bit),
        [Activo] bit NOT NULL CONSTRAINT [DF_Usuario_Activo] DEFAULT CAST(1 AS bit),
        [CreadoPorId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_Usuario_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        [ModificadoPorId] int NULL,
        [FechaModificacion] datetime2(0) NULL,
        CONSTRAINT [PK_Usuario] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Usuario_CreadoPor] FOREIGN KEY ([CreadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Usuario_Empleado] FOREIGN KEY ([EmpleadoId]) REFERENCES [dbo].[Empleado] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Usuario_ModificadoPor] FOREIGN KEY ([ModificadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE TABLE [dbo].[EstadoProyecto] (
        [Id] tinyint NOT NULL,
        [EsVigente] bit NOT NULL,
        [CreadoPorId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_EstadoProyecto_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        [ModificadoPorId] int NULL,
        [FechaModificacion] datetime2(0) NULL,
        [Codigo] varchar(30) NOT NULL,
        [Nombre] nvarchar(100) NOT NULL,
        [Orden] tinyint NOT NULL,
        [Activo] bit NOT NULL CONSTRAINT [DF_EstadoProyecto_Activo] DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_EstadoProyecto] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EstadoProyecto_CreadoPor] FOREIGN KEY ([CreadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_EstadoProyecto_ModificadoPor] FOREIGN KEY ([ModificadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE TABLE [dbo].[GrupoProyecto] (
        [Id] tinyint NOT NULL,
        [RequiereProyectoErp] bit NOT NULL,
        [RequiereDimension] bit NOT NULL,
        [CreadoPorId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_GrupoProyecto_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        [ModificadoPorId] int NULL,
        [FechaModificacion] datetime2(0) NULL,
        [Codigo] varchar(30) NOT NULL,
        [Nombre] nvarchar(100) NOT NULL,
        [Orden] tinyint NOT NULL,
        [Activo] bit NOT NULL CONSTRAINT [DF_GrupoProyecto_Activo] DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_GrupoProyecto] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_GrupoProyecto_Requisito] CHECK ([RequiereProyectoErp] = 1 OR [RequiereDimension] = 1),
        CONSTRAINT [FK_GrupoProyecto_CreadoPor] FOREIGN KEY ([CreadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GrupoProyecto_ModificadoPor] FOREIGN KEY ([ModificadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE TABLE [dbo].[Jornada] (
        [Id] tinyint NOT NULL,
        [DiasTrabajo] tinyint NOT NULL,
        [DiasDescanso] tinyint NOT NULL,
        [CreadoPorId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_Jornada_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        [ModificadoPorId] int NULL,
        [FechaModificacion] datetime2(0) NULL,
        [Codigo] varchar(30) NOT NULL,
        [Nombre] nvarchar(100) NOT NULL,
        [Orden] tinyint NOT NULL,
        [Activo] bit NOT NULL CONSTRAINT [DF_Jornada_Activo] DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_Jornada] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Jornada_Dias] CHECK ([DiasTrabajo] > 0 AND [DiasDescanso] >= 0),
        CONSTRAINT [FK_Jornada_CreadoPor] FOREIGN KEY ([CreadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Jornada_ModificadoPor] FOREIGN KEY ([ModificadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE TABLE [dbo].[OrigenNovedad] (
        [Id] tinyint NOT NULL,
        [EsEditable] bit NOT NULL,
        [CreadoPorId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_OrigenNovedad_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        [ModificadoPorId] int NULL,
        [FechaModificacion] datetime2(0) NULL,
        [Codigo] varchar(30) NOT NULL,
        [Nombre] nvarchar(100) NOT NULL,
        [Orden] tinyint NOT NULL,
        [Activo] bit NOT NULL CONSTRAINT [DF_OrigenNovedad_Activo] DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_OrigenNovedad] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrigenNovedad_CreadoPor] FOREIGN KEY ([CreadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OrigenNovedad_ModificadoPor] FOREIGN KEY ([ModificadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE TABLE [dbo].[Parametro] (
        [Id] int NOT NULL IDENTITY,
        [Clave] varchar(60) NOT NULL,
        [Valor] nvarchar(400) NOT NULL,
        [TipoDato] varchar(10) NOT NULL,
        [Descripcion] nvarchar(300) NULL,
        [CreadoPorId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_Parametro_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        [ModificadoPorId] int NULL,
        [FechaModificacion] datetime2(0) NULL,
        CONSTRAINT [PK_Parametro] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Parametro_TipoDato] CHECK ([TipoDato] IN ('INT','BOOL','TEXT','LIST')),
        CONSTRAINT [FK_Parametro_CreadoPor] FOREIGN KEY ([CreadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Parametro_ModificadoPor] FOREIGN KEY ([ModificadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE TABLE [dbo].[RolAsignacion] (
        [Id] tinyint NOT NULL,
        [EsDescanso] bit NOT NULL,
        [CreadoPorId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_RolAsignacion_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        [ModificadoPorId] int NULL,
        [FechaModificacion] datetime2(0) NULL,
        [Codigo] varchar(30) NOT NULL,
        [Nombre] nvarchar(100) NOT NULL,
        [Orden] tinyint NOT NULL,
        [Activo] bit NOT NULL CONSTRAINT [DF_RolAsignacion_Activo] DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_RolAsignacion] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RolAsignacion_CreadoPor] FOREIGN KEY ([CreadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RolAsignacion_ModificadoPor] FOREIGN KEY ([ModificadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE TABLE [dbo].[TipoAplicacionNovedad] (
        [Id] tinyint NOT NULL,
        [CreadoPorId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_TipoAplicacionNovedad_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        [ModificadoPorId] int NULL,
        [FechaModificacion] datetime2(0) NULL,
        [Codigo] varchar(30) NOT NULL,
        [Nombre] nvarchar(100) NOT NULL,
        [Orden] tinyint NOT NULL,
        [Activo] bit NOT NULL CONSTRAINT [DF_TipoAplicacionNovedad_Activo] DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_TipoAplicacionNovedad] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TipoAplicacionNovedad_CreadoPor] FOREIGN KEY ([CreadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TipoAplicacionNovedad_ModificadoPor] FOREIGN KEY ([ModificadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE TABLE [dbo].[TipoMovimiento] (
        [Id] tinyint NOT NULL,
        [CreadoPorId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_TipoMovimiento_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        [ModificadoPorId] int NULL,
        [FechaModificacion] datetime2(0) NULL,
        [Codigo] varchar(30) NOT NULL,
        [Nombre] nvarchar(100) NOT NULL,
        [Orden] tinyint NOT NULL,
        [Activo] bit NOT NULL CONSTRAINT [DF_TipoMovimiento_Activo] DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_TipoMovimiento] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TipoMovimiento_CreadoPor] FOREIGN KEY ([CreadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TipoMovimiento_ModificadoPor] FOREIGN KEY ([ModificadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE TABLE [dbo].[TipoNovedad] (
        [Id] tinyint NOT NULL,
        [Sigla] varchar(10) NOT NULL,
        [SiglaCronograma] varchar(4) NOT NULL,
        [ColorHex] char(7) NOT NULL,
        [CategoriaId] int NOT NULL,
        [CategoriaDescripcion] nvarchar(100) NOT NULL,
        [EstadoReporte] varchar(20) NOT NULL,
        [ObservacionReporte] nvarchar(60) NOT NULL,
        [AplicaPersona] bit NOT NULL,
        [AplicaProyecto] bit NOT NULL,
        [AplicaGeneral] bit NOT NULL,
        [SeleccionableManual] bit NOT NULL,
        [CreadoPorId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_TipoNovedad_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        [ModificadoPorId] int NULL,
        [FechaModificacion] datetime2(0) NULL,
        [Codigo] varchar(30) NOT NULL,
        [Nombre] nvarchar(100) NOT NULL,
        [Orden] tinyint NOT NULL,
        [Activo] bit NOT NULL CONSTRAINT [DF_TipoNovedad_Activo] DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_TipoNovedad] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_TipoNovedad_ColorHex] CHECK ([ColorHex] LIKE '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]'),
        CONSTRAINT [FK_TipoNovedad_CreadoPor] FOREIGN KEY ([CreadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TipoNovedad_ModificadoPor] FOREIGN KEY ([ModificadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE TABLE [dbo].[UsuarioDepartamento] (
        [Id] int NOT NULL IDENTITY,
        [UsuarioId] int NOT NULL,
        [DepartamentoId] int NOT NULL,
        [Notificado] bit NOT NULL CONSTRAINT [DF_UsuarioDepartamento_Notificado] DEFAULT CAST(0 AS bit),
        [FechaNotificacion] datetime2(0) NULL,
        [Activo] bit NOT NULL CONSTRAINT [DF_UsuarioDepartamento_Activo] DEFAULT CAST(1 AS bit),
        [CreadoPorId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_UsuarioDepartamento_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        [ModificadoPorId] int NULL,
        [FechaModificacion] datetime2(0) NULL,
        CONSTRAINT [PK_UsuarioDepartamento] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UsuarioDepartamento_CreadoPor] FOREIGN KEY ([CreadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UsuarioDepartamento_Departamento] FOREIGN KEY ([DepartamentoId]) REFERENCES [dbo].[Departamento] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UsuarioDepartamento_ModificadoPor] FOREIGN KEY ([ModificadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_UsuarioDepartamento_Usuario] FOREIGN KEY ([UsuarioId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE TABLE [dbo].[Proyecto] (
        [Id] int NOT NULL IDENTITY,
        [Uid] uniqueidentifier NOT NULL CONSTRAINT [DF_Proyecto_Uid] DEFAULT (NEWID()),
        [Codigo] varchar(30) NOT NULL,
        [NombreVisual] nvarchar(300) NOT NULL,
        [GrupoProyectoId] tinyint NOT NULL,
        [CompaniaId] int NOT NULL,
        [ProyectoErpId] varchar(30) NULL,
        [ProyectoErpNombre] nvarchar(300) NULL,
        [ProyectoErpEstado] nvarchar(30) NULL,
        [DimensionUegpId] varchar(30) NULL,
        [DimensionDescripcion] nvarchar(300) NULL,
        [FechaInicio] date NOT NULL,
        [FechaFin] date NOT NULL,
        [EstadoProyectoId] tinyint NOT NULL,
        [HorarioCodigo] int NULL,
        [HorarioDescripcion] nvarchar(200) NULL,
        [HoraEntrada] time(0) NULL,
        [HoraSalida] time(0) NULL,
        [HorasJornadaMin] smallint NULL,
        [HorasTrabajadasMin] smallint NULL,
        [TipoHorario] varchar(5) NULL,
        [SalidaAlmuerzo] time(0) NULL,
        [RegresoAlmuerzo] time(0) NULL,
        [DepartamentoId] int NULL,
        [PropietarioUsuarioId] int NOT NULL,
        [Eliminado] bit NOT NULL CONSTRAINT [DF_Proyecto_Eliminado] DEFAULT CAST(0 AS bit),
        [LegacyId] int NULL,
        [RowVer] rowversion NOT NULL,
        [CreadoPorId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_Proyecto_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        [ModificadoPorId] int NULL,
        [FechaModificacion] datetime2(0) NULL,
        CONSTRAINT [PK_Proyecto] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Proyecto_Almuerzo] CHECK ([SalidaAlmuerzo] IS NULL OR [RegresoAlmuerzo] IS NULL OR [RegresoAlmuerzo] > [SalidaAlmuerzo]),
        CONSTRAINT [CK_Proyecto_Fechas] CHECK ([FechaFin] >= [FechaInicio]),
        CONSTRAINT [CK_Proyecto_Origen] CHECK ([ProyectoErpId] IS NOT NULL OR [DimensionUegpId] IS NOT NULL),
        CONSTRAINT [FK_Proyecto_Compania] FOREIGN KEY ([CompaniaId]) REFERENCES [dbo].[Compania] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Proyecto_CreadoPor] FOREIGN KEY ([CreadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Proyecto_Departamento] FOREIGN KEY ([DepartamentoId]) REFERENCES [dbo].[Departamento] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Proyecto_EstadoProyecto] FOREIGN KEY ([EstadoProyectoId]) REFERENCES [dbo].[EstadoProyecto] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Proyecto_GrupoProyecto] FOREIGN KEY ([GrupoProyectoId]) REFERENCES [dbo].[GrupoProyecto] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Proyecto_ModificadoPor] FOREIGN KEY ([ModificadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Proyecto_Propietario] FOREIGN KEY ([PropietarioUsuarioId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE TABLE [dbo].[Novedad] (
        [Id] int NOT NULL IDENTITY,
        [Uid] uniqueidentifier NOT NULL CONSTRAINT [DF_Novedad_Uid] DEFAULT (NEWID()),
        [Codigo] varchar(30) NOT NULL,
        [TipoAplicacionNovedadId] tinyint NOT NULL,
        [TipoNovedadId] tinyint NOT NULL,
        [OrigenNovedadId] tinyint NOT NULL,
        [EmpleadoId] int NULL,
        [ProyectoId] int NULL,
        [FechaInicio] date NOT NULL,
        [FechaFin] date NOT NULL,
        [Observacion] nvarchar(500) NULL,
        [OrigenArea] nvarchar(30) NULL,
        [RegistradoPorNombre] nvarchar(200) NULL,
        [RegistradoPorCorreo] nvarchar(256) NULL,
        [DetalleOrigenJson] nvarchar(max) NULL,
        [Anulada] bit NOT NULL CONSTRAINT [DF_Novedad_Anulada] DEFAULT CAST(0 AS bit),
        [FechaAnulacion] datetime2(0) NULL,
        [AnuladaPorId] int NULL,
        [MotivoAnulacion] nvarchar(500) NULL,
        [LegacyId] int NULL,
        [RowVer] rowversion NOT NULL,
        [CreadoPorId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_Novedad_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        [ModificadoPorId] int NULL,
        [FechaModificacion] datetime2(0) NULL,
        CONSTRAINT [PK_Novedad] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Novedad_Anulacion] CHECK ([Anulada] = 0 OR ([FechaAnulacion] IS NOT NULL AND [AnuladaPorId] IS NOT NULL)),
        CONSTRAINT [CK_Novedad_Aplicacion] CHECK (([TipoAplicacionNovedadId] = 1 AND [EmpleadoId] IS NOT NULL AND [ProyectoId] IS NULL) OR ([TipoAplicacionNovedadId] = 2 AND [ProyectoId] IS NOT NULL AND [EmpleadoId] IS NULL) OR ([TipoAplicacionNovedadId] = 3 AND [ProyectoId] IS NULL AND [EmpleadoId] IS NULL)),
        CONSTRAINT [CK_Novedad_DetalleJson] CHECK ([DetalleOrigenJson] IS NULL OR ISJSON([DetalleOrigenJson]) = 1),
        CONSTRAINT [CK_Novedad_Fechas] CHECK ([FechaFin] >= [FechaInicio]),
        CONSTRAINT [FK_Novedad_AnuladaPor] FOREIGN KEY ([AnuladaPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Novedad_CreadoPor] FOREIGN KEY ([CreadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Novedad_Empleado] FOREIGN KEY ([EmpleadoId]) REFERENCES [dbo].[Empleado] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Novedad_ModificadoPor] FOREIGN KEY ([ModificadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Novedad_Origen] FOREIGN KEY ([OrigenNovedadId]) REFERENCES [dbo].[OrigenNovedad] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Novedad_Proyecto] FOREIGN KEY ([ProyectoId]) REFERENCES [dbo].[Proyecto] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Novedad_TipoAplicacion] FOREIGN KEY ([TipoAplicacionNovedadId]) REFERENCES [dbo].[TipoAplicacionNovedad] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Novedad_TipoNovedad] FOREIGN KEY ([TipoNovedadId]) REFERENCES [dbo].[TipoNovedad] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE TABLE [dbo].[ProyectoActividad] (
        [Id] int NOT NULL IDENTITY,
        [ProyectoId] int NOT NULL,
        [MovimientoUid] uniqueidentifier NOT NULL CONSTRAINT [DF_ProyectoActividad_Uid] DEFAULT (NEWID()),
        [Version] int NOT NULL,
        [TipoMovimientoId] tinyint NOT NULL,
        [FechaInicio] date NOT NULL,
        [FechaFin] date NOT NULL,
        [ActividadCodigo] varchar(20) NOT NULL,
        [ActividadDescripcion] nvarchar(300) NULL,
        [ActividadTipo] nvarchar(100) NULL,
        [LegacyId] int NULL,
        [CreadoPorId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_ProyectoActividad_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        [ModificadoPorId] int NULL,
        [FechaModificacion] datetime2(0) NULL,
        CONSTRAINT [PK_ProyectoActividad] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ProyectoActividad_Fechas] CHECK ([FechaFin] >= [FechaInicio]),
        CONSTRAINT [FK_ProyectoActividad_CreadoPor] FOREIGN KEY ([CreadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProyectoActividad_ModificadoPor] FOREIGN KEY ([ModificadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProyectoActividad_Proyecto] FOREIGN KEY ([ProyectoId]) REFERENCES [dbo].[Proyecto] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProyectoActividad_TipoMovimiento] FOREIGN KEY ([TipoMovimientoId]) REFERENCES [dbo].[TipoMovimiento] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE TABLE [dbo].[ProyectoEtapa] (
        [Id] int NOT NULL IDENTITY,
        [ProyectoId] int NOT NULL,
        [EtapaUid] uniqueidentifier NOT NULL CONSTRAINT [DF_ProyectoEtapa_EtapaUid] DEFAULT (NEWID()),
        [Version] int NOT NULL,
        [TipoMovimientoId] tinyint NOT NULL,
        [EstadoProyectoId] tinyint NOT NULL,
        [FechaInicio] date NOT NULL,
        [FechaFin] date NOT NULL,
        [FechaCorte] date NULL,
        [ActividadCodigo] varchar(20) NULL,
        [SnapshotPersonal] nvarchar(max) NULL,
        [LegacyId] int NULL,
        [CreadoPorId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_ProyectoEtapa_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        [ModificadoPorId] int NULL,
        [FechaModificacion] datetime2(0) NULL,
        CONSTRAINT [PK_ProyectoEtapa] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ProyectoEtapa_Fechas] CHECK ([FechaFin] >= [FechaInicio]),
        CONSTRAINT [CK_ProyectoEtapa_Snapshot] CHECK ([SnapshotPersonal] IS NULL OR ISJSON([SnapshotPersonal]) = 1),
        CONSTRAINT [FK_ProyectoEtapa_CreadoPor] FOREIGN KEY ([CreadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProyectoEtapa_Estado] FOREIGN KEY ([EstadoProyectoId]) REFERENCES [dbo].[EstadoProyecto] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProyectoEtapa_ModificadoPor] FOREIGN KEY ([ModificadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProyectoEtapa_Proyecto] FOREIGN KEY ([ProyectoId]) REFERENCES [dbo].[Proyecto] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProyectoEtapa_TipoMovimiento] FOREIGN KEY ([TipoMovimientoId]) REFERENCES [dbo].[TipoMovimiento] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE TABLE [dbo].[ProyectoPersonal] (
        [Id] int NOT NULL IDENTITY,
        [ProyectoId] int NOT NULL,
        [RolAsignacionId] tinyint NOT NULL,
        [Numero] smallint NOT NULL,
        [EmpleadoId] int NOT NULL,
        [CargoAsignado] nvarchar(200) NULL,
        [FechaInicio] date NOT NULL,
        [FechaFin] date NOT NULL,
        [JornadaId] tinyint NULL,
        [DiasTrabajo] tinyint NULL,
        [DiasDescanso] tinyint NOT NULL CONSTRAINT [DF_ProyectoPersonal_DiasDescanso] DEFAULT CAST(0 AS tinyint),
        [EsPrincipalInicial] bit NOT NULL CONSTRAINT [DF_ProyectoPersonal_EsPrincipalInicial] DEFAULT CAST(0 AS bit),
        [TipoRegistro] varchar(10) NOT NULL CONSTRAINT [DF_ProyectoPersonal_TipoRegistro] DEFAULT 'JORNADA',
        [PrincipalRelacionadoId] int NULL,
        [Observacion] nvarchar(500) NULL,
        [LegacyId] int NULL,
        [CreadoPorId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_ProyectoPersonal_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        [ModificadoPorId] int NULL,
        [FechaModificacion] datetime2(0) NULL,
        CONSTRAINT [PK_ProyectoPersonal] PRIMARY KEY ([Id]),
        CONSTRAINT [UQ_ProyectoPersonal_Clave] UNIQUE ([Id], [ProyectoId], [EmpleadoId]),
        CONSTRAINT [CK_ProyectoPersonal_Fechas] CHECK ([FechaFin] >= [FechaInicio]),
        CONSTRAINT [CK_ProyectoPersonal_Inicial] CHECK ([EsPrincipalInicial] = 0 OR [RolAsignacionId] = 1),
        CONSTRAINT [CK_ProyectoPersonal_Jornada] CHECK ([RolAsignacionId] <> 1 OR ([JornadaId] IS NOT NULL AND [DiasTrabajo] > 0)),
        CONSTRAINT [CK_ProyectoPersonal_Rol] CHECK ([RolAsignacionId] IN (1, 2)),
        CONSTRAINT [CK_ProyectoPersonal_TipoRegistro] CHECK ([TipoRegistro] IN ('JORNADA','DESCANSO')),
        CONSTRAINT [FK_ProyectoPersonal_CreadoPor] FOREIGN KEY ([CreadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProyectoPersonal_Empleado] FOREIGN KEY ([EmpleadoId]) REFERENCES [dbo].[Empleado] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProyectoPersonal_Jornada] FOREIGN KEY ([JornadaId]) REFERENCES [dbo].[Jornada] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProyectoPersonal_ModificadoPor] FOREIGN KEY ([ModificadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProyectoPersonal_Principal] FOREIGN KEY ([PrincipalRelacionadoId]) REFERENCES [dbo].[ProyectoPersonal] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProyectoPersonal_Proyecto] FOREIGN KEY ([ProyectoId]) REFERENCES [dbo].[Proyecto] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProyectoPersonal_Rol] FOREIGN KEY ([RolAsignacionId]) REFERENCES [dbo].[RolAsignacion] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE TABLE [dbo].[NovedadDia] (
        [Id] bigint NOT NULL IDENTITY,
        [NovedadId] int NOT NULL,
        [Fecha] date NOT NULL,
        [CreadoPorId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_NovedadDia_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        [ModificadoPorId] int NULL,
        [FechaModificacion] datetime2(0) NULL,
        CONSTRAINT [PK_NovedadDia] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NovedadDia_CreadoPor] FOREIGN KEY ([CreadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_NovedadDia_ModificadoPor] FOREIGN KEY ([ModificadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_NovedadDia_Novedad] FOREIGN KEY ([NovedadId]) REFERENCES [dbo].[Novedad] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE TABLE [dbo].[ProyectoAsignacionDia] (
        [Id] bigint NOT NULL IDENTITY,
        [ProyectoId] int NOT NULL,
        [ProyectoPersonalId] int NOT NULL,
        [EmpleadoId] int NOT NULL,
        [Fecha] date NOT NULL,
        [RolAsignacionId] tinyint NOT NULL,
        [TipoAsignacion] varchar(6) NOT NULL,
        [Bloque] smallint NOT NULL,
        [LegacyId] int NULL,
        [CreadoPorId] int NOT NULL,
        [FechaCreacion] datetime2(0) NOT NULL CONSTRAINT [DF_ProyectoAsignacionDia_FechaCreacion] DEFAULT (SYSUTCDATETIME()),
        [ModificadoPorId] int NULL,
        [FechaModificacion] datetime2(0) NULL,
        CONSTRAINT [PK_ProyectoAsignacionDia] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ProyectoAsignacionDia_Tipo] CHECK ([TipoAsignacion] IN ('AUTO','MANUAL')),
        CONSTRAINT [FK_ProyectoAsignacionDia_CreadoPor] FOREIGN KEY ([CreadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProyectoAsignacionDia_ModificadoPor] FOREIGN KEY ([ModificadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProyectoAsignacionDia_Personal] FOREIGN KEY ([ProyectoPersonalId], [ProyectoId], [EmpleadoId]) REFERENCES [dbo].[ProyectoPersonal] ([Id], [ProyectoId], [EmpleadoId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProyectoAsignacionDia_Proyecto] FOREIGN KEY ([ProyectoId]) REFERENCES [dbo].[Proyecto] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProyectoAsignacionDia_Rol] FOREIGN KEY ([RolAsignacionId]) REFERENCES [dbo].[RolAsignacion] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'CreadoPorId', N'Email', N'EmpleadoId', N'EntraObjectId', N'EsSistema', N'FechaCreacion', N'FechaModificacion', N'ModificadoPorId', N'NombreMostrar') AND [object_id] = OBJECT_ID(N'[dbo].[Usuario]'))
        SET IDENTITY_INSERT [dbo].[Usuario] ON;
    EXEC(N'INSERT INTO [dbo].[Usuario] ([Id], [Activo], [CreadoPorId], [Email], [EmpleadoId], [EntraObjectId], [EsSistema], [FechaCreacion], [FechaModificacion], [ModificadoPorId], [NombreMostrar])
    VALUES (1, CAST(1 AS bit), 1, N''sistema@profesiograma.local'', NULL, NULL, CAST(1 AS bit), ''2026-09-29T00:00:00Z'', NULL, NULL, N''SISTEMA'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'CreadoPorId', N'Email', N'EmpleadoId', N'EntraObjectId', N'EsSistema', N'FechaCreacion', N'FechaModificacion', N'ModificadoPorId', N'NombreMostrar') AND [object_id] = OBJECT_ID(N'[dbo].[Usuario]'))
        SET IDENTITY_INSERT [dbo].[Usuario] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'Cargo', N'CodigoInfor', N'CreadoPorId', N'FechaCreacion', N'FechaModificacion', N'ModificadoPorId') AND [object_id] = OBJECT_ID(N'[dbo].[CargoInfor]'))
        SET IDENTITY_INSERT [dbo].[CargoInfor] ON;
    EXEC(N'INSERT INTO [dbo].[CargoInfor] ([Id], [Activo], [Cargo], [CodigoInfor], [CreadoPorId], [FechaCreacion], [FechaModificacion], [ModificadoPorId])
    VALUES (1, CAST(1 AS bit), N''PARAMEDICO'', ''P0021'', 1, ''2026-09-29T00:00:00Z'', NULL, NULL),
    (2, CAST(1 AS bit), N''DESARROLLADOR DE SOFTWARE'', ''PPOOPP'', 1, ''2026-09-29T00:00:00Z'', NULL, NULL),
    (3, CAST(1 AS bit), N''SUPERVISOR SSA'', ''P00030'', 1, ''2026-09-29T00:00:00Z'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'Cargo', N'CodigoInfor', N'CreadoPorId', N'FechaCreacion', N'FechaModificacion', N'ModificadoPorId') AND [object_id] = OBJECT_ID(N'[dbo].[CargoInfor]'))
        SET IDENTITY_INSERT [dbo].[CargoInfor] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'CodigoErp', N'CreadoPorId', N'FechaCreacion', N'FechaModificacion', N'ModificadoPorId', N'Nombre', N'NombreCorto', N'Orden', N'Tipo') AND [object_id] = OBJECT_ID(N'[dbo].[Departamento]'))
        SET IDENTITY_INSERT [dbo].[Departamento] ON;
    EXEC(N'INSERT INTO [dbo].[Departamento] ([Id], [Activo], [CodigoErp], [CreadoPorId], [FechaCreacion], [FechaModificacion], [ModificadoPorId], [Nombre], [NombreCorto], [Orden], [Tipo])
    VALUES (1, CAST(1 AS bit), NULL, 1, ''2026-09-29T00:00:00Z'', NULL, NULL, N''DEPARTAMENTO DE INFRAESTRUCTURA'', N''Infraestructura Integral'', CAST(1 AS smallint), ''DEPARTAMENTO''),
    (2, CAST(1 AS bit), NULL, 1, ''2026-09-29T00:00:00Z'', NULL, NULL, N''DEPARTAMENTO SEDEMI TELECOM'', N''Telecomunicaciones'', CAST(2 AS smallint), ''DEPARTAMENTO''),
    (3, CAST(1 AS bit), NULL, 1, ''2026-09-29T00:00:00Z'', NULL, NULL, N''DEPARTAMENTO SEDEMI PETROLEO Y GAS'', N''Petróleo y Gas'', CAST(3 AS smallint), ''DEPARTAMENTO''),
    (4, CAST(1 AS bit), NULL, 1, ''2026-09-29T00:00:00Z'', NULL, NULL, N''DEPARTAMENTO SEDEMI ENERGIA'', N''Energía'', CAST(4 AS smallint), ''DEPARTAMENTO''),
    (5, CAST(1 AS bit), NULL, 1, ''2026-09-29T00:00:00Z'', NULL, NULL, N''DEPARTAMENTO DE INFRAESTRUCTURA METALICA'', N''Infraestructura Metálica'', CAST(5 AS smallint), ''DEPARTAMENTO''),
    (6, CAST(1 AS bit), NULL, 1, ''2026-09-29T00:00:00Z'', NULL, NULL, N''DEPARTAMENTO SEDEMI MINERIA'', N''Minería'', CAST(6 AS smallint), ''DEPARTAMENTO''),
    (7, CAST(1 AS bit), NULL, 1, ''2026-09-29T00:00:00Z'', NULL, NULL, N''UNIDAD SISTEMA INTEGRADO DE GESTION'', N''SIG'', CAST(7 AS smallint), ''UNIDAD'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'CodigoErp', N'CreadoPorId', N'FechaCreacion', N'FechaModificacion', N'ModificadoPorId', N'Nombre', N'NombreCorto', N'Orden', N'Tipo') AND [object_id] = OBJECT_ID(N'[dbo].[Departamento]'))
        SET IDENTITY_INSERT [dbo].[Departamento] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'Codigo', N'CreadoPorId', N'EsVigente', N'FechaCreacion', N'FechaModificacion', N'ModificadoPorId', N'Nombre', N'Orden') AND [object_id] = OBJECT_ID(N'[dbo].[EstadoProyecto]'))
        SET IDENTITY_INSERT [dbo].[EstadoProyecto] ON;
    EXEC(N'INSERT INTO [dbo].[EstadoProyecto] ([Id], [Activo], [Codigo], [CreadoPorId], [EsVigente], [FechaCreacion], [FechaModificacion], [ModificadoPorId], [Nombre], [Orden])
    VALUES (CAST(1 AS tinyint), CAST(1 AS bit), ''ACTIVO'', 1, CAST(1 AS bit), ''2026-09-29T00:00:00Z'', NULL, NULL, N''Activo'', CAST(1 AS tinyint)),
    (CAST(2 AS tinyint), CAST(1 AS bit), ''SUSPENDIDO'', 1, CAST(1 AS bit), ''2026-09-29T00:00:00Z'', NULL, NULL, N''Suspendido'', CAST(2 AS tinyint)),
    (CAST(3 AS tinyint), CAST(1 AS bit), ''INACTIVO'', 1, CAST(0 AS bit), ''2026-09-29T00:00:00Z'', NULL, NULL, N''Inactivo'', CAST(3 AS tinyint)),
    (CAST(4 AS tinyint), CAST(1 AS bit), ''TERMINADO'', 1, CAST(0 AS bit), ''2026-09-29T00:00:00Z'', NULL, NULL, N''Terminado'', CAST(4 AS tinyint))');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'Codigo', N'CreadoPorId', N'EsVigente', N'FechaCreacion', N'FechaModificacion', N'ModificadoPorId', N'Nombre', N'Orden') AND [object_id] = OBJECT_ID(N'[dbo].[EstadoProyecto]'))
        SET IDENTITY_INSERT [dbo].[EstadoProyecto] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'Codigo', N'CreadoPorId', N'FechaCreacion', N'FechaModificacion', N'ModificadoPorId', N'Nombre', N'Orden', N'RequiereDimension', N'RequiereProyectoErp') AND [object_id] = OBJECT_ID(N'[dbo].[GrupoProyecto]'))
        SET IDENTITY_INSERT [dbo].[GrupoProyecto] ON;
    EXEC(N'INSERT INTO [dbo].[GrupoProyecto] ([Id], [Activo], [Codigo], [CreadoPorId], [FechaCreacion], [FechaModificacion], [ModificadoPorId], [Nombre], [Orden], [RequiereDimension], [RequiereProyectoErp])
    VALUES (CAST(1 AS tinyint), CAST(1 AS bit), ''CAMPO'', 1, ''2026-09-29T00:00:00Z'', NULL, NULL, N''CAMPO'', CAST(1 AS tinyint), CAST(0 AS bit), CAST(1 AS bit)),
    (CAST(2 AS tinyint), CAST(1 AS bit), ''PLANTA'', 1, ''2026-09-29T00:00:00Z'', NULL, NULL, N''PLANTA'', CAST(2 AS tinyint), CAST(1 AS bit), CAST(0 AS bit)),
    (CAST(3 AS tinyint), CAST(1 AS bit), ''OFICINAS ADMINISTRATIVAS'', 1, ''2026-09-29T00:00:00Z'', NULL, NULL, N''OFICINAS ADMINISTRATIVAS'', CAST(3 AS tinyint), CAST(1 AS bit), CAST(0 AS bit))');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'Codigo', N'CreadoPorId', N'FechaCreacion', N'FechaModificacion', N'ModificadoPorId', N'Nombre', N'Orden', N'RequiereDimension', N'RequiereProyectoErp') AND [object_id] = OBJECT_ID(N'[dbo].[GrupoProyecto]'))
        SET IDENTITY_INSERT [dbo].[GrupoProyecto] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'Codigo', N'CreadoPorId', N'DiasDescanso', N'DiasTrabajo', N'FechaCreacion', N'FechaModificacion', N'ModificadoPorId', N'Nombre', N'Orden') AND [object_id] = OBJECT_ID(N'[dbo].[Jornada]'))
        SET IDENTITY_INSERT [dbo].[Jornada] ON;
    EXEC(N'INSERT INTO [dbo].[Jornada] ([Id], [Activo], [Codigo], [CreadoPorId], [DiasDescanso], [DiasTrabajo], [FechaCreacion], [FechaModificacion], [ModificadoPorId], [Nombre], [Orden])
    VALUES (CAST(1 AS tinyint), CAST(1 AS bit), ''TIPO_1'', 1, CAST(8 AS tinyint), CAST(22 AS tinyint), ''2026-09-29T00:00:00Z'', NULL, NULL, N''Tipo 1 (22-8)'', CAST(1 AS tinyint)),
    (CAST(2 AS tinyint), CAST(1 AS bit), ''TIPO_2'', 1, CAST(4 AS tinyint), CAST(11 AS tinyint), ''2026-09-29T00:00:00Z'', NULL, NULL, N''Tipo 2 (11-4)'', CAST(2 AS tinyint)),
    (CAST(3 AS tinyint), CAST(1 AS bit), ''TIPO_3'', 1, CAST(2 AS tinyint), CAST(5 AS tinyint), ''2026-09-29T00:00:00Z'', NULL, NULL, N''Tipo 3 (5-2)'', CAST(3 AS tinyint)),
    (CAST(4 AS tinyint), CAST(1 AS bit), ''ESPECIAL'', 1, CAST(0 AS tinyint), CAST(3 AS tinyint), ''2026-09-29T00:00:00Z'', NULL, NULL, N''Especiales (3 días o menos)'', CAST(4 AS tinyint))');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'Codigo', N'CreadoPorId', N'DiasDescanso', N'DiasTrabajo', N'FechaCreacion', N'FechaModificacion', N'ModificadoPorId', N'Nombre', N'Orden') AND [object_id] = OBJECT_ID(N'[dbo].[Jornada]'))
        SET IDENTITY_INSERT [dbo].[Jornada] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'Codigo', N'CreadoPorId', N'EsEditable', N'FechaCreacion', N'FechaModificacion', N'ModificadoPorId', N'Nombre', N'Orden') AND [object_id] = OBJECT_ID(N'[dbo].[OrigenNovedad]'))
        SET IDENTITY_INSERT [dbo].[OrigenNovedad] ON;
    EXEC(N'INSERT INTO [dbo].[OrigenNovedad] ([Id], [Activo], [Codigo], [CreadoPorId], [EsEditable], [FechaCreacion], [FechaModificacion], [ModificadoPorId], [Nombre], [Orden])
    VALUES (CAST(1 AS tinyint), CAST(1 AS bit), ''PROFESIOGRAMA'', 1, CAST(1 AS bit), ''2026-09-29T00:00:00Z'', NULL, NULL, N''PROFESIOGRAMA'', CAST(1 AS tinyint)),
    (CAST(2 AS tinyint), CAST(1 AS bit), ''PERMISOS_MEDICOS'', 1, CAST(0 AS bit), ''2026-09-29T00:00:00Z'', NULL, NULL, N''App Permisos Médicos'', CAST(2 AS tinyint))');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'Codigo', N'CreadoPorId', N'EsEditable', N'FechaCreacion', N'FechaModificacion', N'ModificadoPorId', N'Nombre', N'Orden') AND [object_id] = OBJECT_ID(N'[dbo].[OrigenNovedad]'))
        SET IDENTITY_INSERT [dbo].[OrigenNovedad] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Clave', N'CreadoPorId', N'Descripcion', N'FechaCreacion', N'FechaModificacion', N'ModificadoPorId', N'TipoDato', N'Valor') AND [object_id] = OBJECT_ID(N'[dbo].[Parametro]'))
        SET IDENTITY_INSERT [dbo].[Parametro] ON;
    EXEC(N'INSERT INTO [dbo].[Parametro] ([Id], [Clave], [CreadoPorId], [Descripcion], [FechaCreacion], [FechaModificacion], [ModificadoPorId], [TipoDato], [Valor])
    VALUES (1, ''PROYECTO_MAX_PRINCIPALES'', 1, N''Máximo de principales por proyecto'', ''2026-09-29T00:00:00Z'', NULL, NULL, ''INT'', N''20''),
    (2, ''PROYECTO_MAX_BACKS'', 1, N''Máximo de backs por proyecto'', ''2026-09-29T00:00:00Z'', NULL, NULL, ''INT'', N''20''),
    (3, ''BACK_MAX_DIAS_DESCANSO'', 1, N''Máximo de días de descanso posteriores de un back'', ''2026-09-29T00:00:00Z'', NULL, NULL, ''INT'', N''20''),
    (4, ''CRONOGRAMA_MAX_DIAS'', 1, N''Rango máximo del cronograma en días'', ''2026-09-29T00:00:00Z'', NULL, NULL, ''INT'', N''90''),
    (5, ''PROYECTO_VISIBILIDAD'', 1, N''CREADOR = solo el propietario; DEPARTAMENTO = todo el departamento del proyecto'', ''2026-09-29T00:00:00Z'', NULL, NULL, ''TEXT'', N''CREADOR''),
    (6, ''ALMUERZO_SALIDA_OPCIONES'', 1, N''Horas permitidas de salida a almuerzo'', ''2026-09-29T00:00:00Z'', NULL, NULL, ''LIST'', N''11:00,12:00,13:00,14:00''),
    (7, ''ALMUERZO_REGRESO_OPCIONES'', 1, N''Horas permitidas de regreso de almuerzo'', ''2026-09-29T00:00:00Z'', NULL, NULL, ''LIST'', N''12:00,13:00,14:00,15:00''),
    (8, ''EMPLEADO_FAMILIAS_ASIGNABLES'', 1, N''Familias de puesto que se pueden asignar a proyectos'', ''2026-09-29T00:00:00Z'', NULL, NULL, ''LIST'', N''ADMINISTRATIVO''),
    (9, ''ZONA_HORARIA'', 1, N''Zona horaria de negocio (Ecuador, UTC-5)'', ''2026-09-29T00:00:00Z'', NULL, NULL, ''TEXT'', N''SA Pacific Standard Time'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Clave', N'CreadoPorId', N'Descripcion', N'FechaCreacion', N'FechaModificacion', N'ModificadoPorId', N'TipoDato', N'Valor') AND [object_id] = OBJECT_ID(N'[dbo].[Parametro]'))
        SET IDENTITY_INSERT [dbo].[Parametro] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'Codigo', N'CreadoPorId', N'EsDescanso', N'FechaCreacion', N'FechaModificacion', N'ModificadoPorId', N'Nombre', N'Orden') AND [object_id] = OBJECT_ID(N'[dbo].[RolAsignacion]'))
        SET IDENTITY_INSERT [dbo].[RolAsignacion] ON;
    EXEC(N'INSERT INTO [dbo].[RolAsignacion] ([Id], [Activo], [Codigo], [CreadoPorId], [EsDescanso], [FechaCreacion], [FechaModificacion], [ModificadoPorId], [Nombre], [Orden])
    VALUES (CAST(1 AS tinyint), CAST(1 AS bit), ''PRINCIPAL'', 1, CAST(0 AS bit), ''2026-09-29T00:00:00Z'', NULL, NULL, N''Principal'', CAST(1 AS tinyint)),
    (CAST(2 AS tinyint), CAST(1 AS bit), ''BACK'', 1, CAST(0 AS bit), ''2026-09-29T00:00:00Z'', NULL, NULL, N''Back'', CAST(2 AS tinyint)),
    (CAST(3 AS tinyint), CAST(1 AS bit), ''DESCANSO'', 1, CAST(1 AS bit), ''2026-09-29T00:00:00Z'', NULL, NULL, N''Descanso'', CAST(3 AS tinyint))');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'Codigo', N'CreadoPorId', N'EsDescanso', N'FechaCreacion', N'FechaModificacion', N'ModificadoPorId', N'Nombre', N'Orden') AND [object_id] = OBJECT_ID(N'[dbo].[RolAsignacion]'))
        SET IDENTITY_INSERT [dbo].[RolAsignacion] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'Codigo', N'CreadoPorId', N'FechaCreacion', N'FechaModificacion', N'ModificadoPorId', N'Nombre', N'Orden') AND [object_id] = OBJECT_ID(N'[dbo].[TipoAplicacionNovedad]'))
        SET IDENTITY_INSERT [dbo].[TipoAplicacionNovedad] ON;
    EXEC(N'INSERT INTO [dbo].[TipoAplicacionNovedad] ([Id], [Activo], [Codigo], [CreadoPorId], [FechaCreacion], [FechaModificacion], [ModificadoPorId], [Nombre], [Orden])
    VALUES (CAST(1 AS tinyint), CAST(1 AS bit), ''PERSONA'', 1, ''2026-09-29T00:00:00Z'', NULL, NULL, N''Persona'', CAST(1 AS tinyint)),
    (CAST(2 AS tinyint), CAST(1 AS bit), ''PROYECTO'', 1, ''2026-09-29T00:00:00Z'', NULL, NULL, N''Proyecto'', CAST(2 AS tinyint)),
    (CAST(3 AS tinyint), CAST(1 AS bit), ''GENERAL'', 1, ''2026-09-29T00:00:00Z'', NULL, NULL, N''General'', CAST(3 AS tinyint))');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'Codigo', N'CreadoPorId', N'FechaCreacion', N'FechaModificacion', N'ModificadoPorId', N'Nombre', N'Orden') AND [object_id] = OBJECT_ID(N'[dbo].[TipoAplicacionNovedad]'))
        SET IDENTITY_INSERT [dbo].[TipoAplicacionNovedad] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'Codigo', N'CreadoPorId', N'FechaCreacion', N'FechaModificacion', N'ModificadoPorId', N'Nombre', N'Orden') AND [object_id] = OBJECT_ID(N'[dbo].[TipoMovimiento]'))
        SET IDENTITY_INSERT [dbo].[TipoMovimiento] ON;
    EXEC(N'INSERT INTO [dbo].[TipoMovimiento] ([Id], [Activo], [Codigo], [CreadoPorId], [FechaCreacion], [FechaModificacion], [ModificadoPorId], [Nombre], [Orden])
    VALUES (CAST(1 AS tinyint), CAST(1 AS bit), ''CREACION'', 1, ''2026-09-29T00:00:00Z'', NULL, NULL, N''Creación'', CAST(1 AS tinyint)),
    (CAST(2 AS tinyint), CAST(1 AS bit), ''ACTUALIZACION_PERSONAL'', 1, ''2026-09-29T00:00:00Z'', NULL, NULL, N''Actualización de personal'', CAST(2 AS tinyint)),
    (CAST(3 AS tinyint), CAST(1 AS bit), ''SUSPENSION'', 1, ''2026-09-29T00:00:00Z'', NULL, NULL, N''Suspensión'', CAST(3 AS tinyint)),
    (CAST(4 AS tinyint), CAST(1 AS bit), ''CIERRE'', 1, ''2026-09-29T00:00:00Z'', NULL, NULL, N''Cierre'', CAST(4 AS tinyint)),
    (CAST(5 AS tinyint), CAST(1 AS bit), ''REACTIVACION'', 1, ''2026-09-29T00:00:00Z'', NULL, NULL, N''Reactivación'', CAST(5 AS tinyint)),
    (CAST(6 AS tinyint), CAST(1 AS bit), ''CAMBIO_ACTIVIDAD'', 1, ''2026-09-29T00:00:00Z'', NULL, NULL, N''Cambio de actividad'', CAST(6 AS tinyint)),
    (CAST(7 AS tinyint), CAST(1 AS bit), ''EDICION_CABECERA'', 1, ''2026-09-29T00:00:00Z'', NULL, NULL, N''Edición de datos generales'', CAST(7 AS tinyint))');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'Codigo', N'CreadoPorId', N'FechaCreacion', N'FechaModificacion', N'ModificadoPorId', N'Nombre', N'Orden') AND [object_id] = OBJECT_ID(N'[dbo].[TipoMovimiento]'))
        SET IDENTITY_INSERT [dbo].[TipoMovimiento] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'AplicaGeneral', N'AplicaPersona', N'AplicaProyecto', N'CategoriaDescripcion', N'CategoriaId', N'Codigo', N'ColorHex', N'CreadoPorId', N'EstadoReporte', N'FechaCreacion', N'FechaModificacion', N'ModificadoPorId', N'Nombre', N'ObservacionReporte', N'Orden', N'SeleccionableManual', N'Sigla', N'SiglaCronograma') AND [object_id] = OBJECT_ID(N'[dbo].[TipoNovedad]'))
        SET IDENTITY_INSERT [dbo].[TipoNovedad] ON;
    EXEC(N'INSERT INTO [dbo].[TipoNovedad] ([Id], [Activo], [AplicaGeneral], [AplicaPersona], [AplicaProyecto], [CategoriaDescripcion], [CategoriaId], [Codigo], [ColorHex], [CreadoPorId], [EstadoReporte], [FechaCreacion], [FechaModificacion], [ModificadoPorId], [Nombre], [ObservacionReporte], [Orden], [SeleccionableManual], [Sigla], [SiglaCronograma])
    VALUES (CAST(1 AS tinyint), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), N''Asistencia Libre'', 5, ''CALAMIDAD_DOMESTICA'', ''#8E44AD'', 1, ''LIBRE'', ''2026-09-29T00:00:00Z'', NULL, NULL, N''CALAMIDAD DOMESTICA'', N''CALAMIDAD DOMESTICA'', CAST(1 AS tinyint), CAST(1 AS bit), ''CD'', ''CD''),
    (CAST(2 AS tinyint), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), N''Asistencia Libre'', 5, ''DESCANSO'', ''#607D8B'', 1, ''LIBRE'', ''2026-09-29T00:00:00Z'', NULL, NULL, N''DESCANSO'', N''DESCANSO'', CAST(2 AS tinyint), CAST(0 AS bit), ''D'', ''D''),
    (CAST(3 AS tinyint), CAST(1 AS bit), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), N''Asistencia Libre'', 5, ''FERIADO'', ''#D13438'', 1, ''LIBRE'', ''2026-09-29T00:00:00Z'', NULL, NULL, N''FERIADO'', N''FERIADO'', CAST(3 AS tinyint), CAST(1 AS bit), ''FER'', ''F''),
    (CAST(4 AS tinyint), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), N''Asistencia Libre'', 5, ''PATERNIDAD'', ''#27AE60'', 1, ''LIBRE'', ''2026-09-29T00:00:00Z'', NULL, NULL, N''PATERNIDAD'', N''PATERNIDAD'', CAST(4 AS tinyint), CAST(1 AS bit), ''PTNDAD'', ''PT''),
    (CAST(5 AS tinyint), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), N''Asistencia Libre'', 5, ''PERMISO'', ''#FFB900'', 1, ''LIBRE'', ''2026-09-29T00:00:00Z'', NULL, NULL, N''PERMISO'', N''PERMISO'', CAST(5 AS tinyint), CAST(1 AS bit), ''P'', ''P''),
    (CAST(6 AS tinyint), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), N''Asistencia Libre'', 5, ''VACACIONES'', ''#0277BD'', 1, ''LIBRE'', ''2026-09-29T00:00:00Z'', NULL, NULL, N''VACACIONES'', N''VACACIONES'', CAST(6 AS tinyint), CAST(1 AS bit), ''V'', ''V''),
    (CAST(7 AS tinyint), CAST(1 AS bit), CAST(0 AS bit), CAST(1 AS bit), CAST(0 AS bit), N''Asistencia Libre'', 5, ''PERMISO_MEDICO'', ''#FFB900'', 1, ''LIBRE'', ''2026-09-29T00:00:00Z'', NULL, NULL, N''PERMISO MEDICO'', N''PERMISO MEDICO'', CAST(7 AS tinyint), CAST(0 AS bit), ''PM'', ''PM'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Activo', N'AplicaGeneral', N'AplicaPersona', N'AplicaProyecto', N'CategoriaDescripcion', N'CategoriaId', N'Codigo', N'ColorHex', N'CreadoPorId', N'EstadoReporte', N'FechaCreacion', N'FechaModificacion', N'ModificadoPorId', N'Nombre', N'ObservacionReporte', N'Orden', N'SeleccionableManual', N'Sigla', N'SiglaCronograma') AND [object_id] = OBJECT_ID(N'[dbo].[TipoNovedad]'))
        SET IDENTITY_INSERT [dbo].[TipoNovedad] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_CargoInfor_Cargo] ON [dbo].[CargoInfor] ([Cargo]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Departamento_Nombre] ON [dbo].[Departamento] ([Nombre]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE INDEX [IX_Empleado_Busqueda] ON [dbo].[Empleado] ([EstadoErp], [FamiliaPuesto]) INCLUDE ([NombreCompleto], [CodigoEkon], [Departamento], [Unidad], [Puesto]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE INDEX [IX_Empleado_Cedula] ON [dbo].[Empleado] ([Cedula]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE INDEX [IX_Empleado_CorreoEmpresa] ON [dbo].[Empleado] ([CorreoEmpresa]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Empleado_CodigoEkon] ON [dbo].[Empleado] ([CodigoEkon]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_EstadoProyecto_Codigo] ON [dbo].[EstadoProyecto] ([Codigo]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_GrupoProyecto_Codigo] ON [dbo].[GrupoProyecto] ([Codigo]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Jornada_Codigo] ON [dbo].[Jornada] ([Codigo]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Novedad_Empleado] ON [dbo].[Novedad] ([EmpleadoId]) WHERE [EmpleadoId] IS NOT NULL AND [Anulada] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Novedad_Proyecto] ON [dbo].[Novedad] ([ProyectoId]) WHERE [ProyectoId] IS NOT NULL AND [Anulada] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Novedad_Rango] ON [dbo].[Novedad] ([FechaInicio], [FechaFin]) INCLUDE ([TipoAplicacionNovedadId], [TipoNovedadId], [EmpleadoId], [ProyectoId]) WHERE [Anulada] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Novedad_Codigo] ON [dbo].[Novedad] ([Codigo]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Novedad_Uid] ON [dbo].[Novedad] ([Uid]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Novedad_LegacyId] ON [dbo].[Novedad] ([LegacyId], [OrigenNovedadId]) WHERE [LegacyId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE INDEX [IX_NovedadDia_Fecha] ON [dbo].[NovedadDia] ([Fecha]) INCLUDE ([NovedadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_NovedadDia] ON [dbo].[NovedadDia] ([NovedadId], [Fecha]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_OrigenNovedad_Codigo] ON [dbo].[OrigenNovedad] ([Codigo]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Parametro_Clave] ON [dbo].[Parametro] ([Clave]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Proyecto_Departamento] ON [dbo].[Proyecto] ([DepartamentoId]) WHERE [Eliminado] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Proyecto_Estado] ON [dbo].[Proyecto] ([EstadoProyectoId]) INCLUDE ([Codigo], [NombreVisual], [FechaInicio], [FechaFin]) WHERE [Eliminado] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE INDEX [IX_Proyecto_Nombre] ON [dbo].[Proyecto] ([NombreVisual]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Proyecto_Propietario] ON [dbo].[Proyecto] ([PropietarioUsuarioId]) WHERE [Eliminado] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Proyecto_Codigo] ON [dbo].[Proyecto] ([Codigo]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Proyecto_Uid] ON [dbo].[Proyecto] ([Uid]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Proyecto_LegacyId] ON [dbo].[Proyecto] ([LegacyId]) WHERE [LegacyId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE INDEX [IX_ProyectoActividad_Vigencia] ON [dbo].[ProyectoActividad] ([ProyectoId], [FechaInicio], [FechaFin]) INCLUDE ([ActividadCodigo], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_ProyectoActividad_Uid] ON [dbo].[ProyectoActividad] ([MovimientoUid]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_ProyectoActividad_Version] ON [dbo].[ProyectoActividad] ([ProyectoId], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ProyectoActividad_LegacyId] ON [dbo].[ProyectoActividad] ([LegacyId]) WHERE [LegacyId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE INDEX [IX_ProyectoAsignacionDia_Cronograma] ON [dbo].[ProyectoAsignacionDia] ([Fecha]) INCLUDE ([ProyectoId], [EmpleadoId], [RolAsignacionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE INDEX [IX_ProyectoAsignacionDia_Cruces] ON [dbo].[ProyectoAsignacionDia] ([EmpleadoId], [Fecha]) INCLUDE ([ProyectoId], [RolAsignacionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE INDEX [IX_ProyectoAsignacionDia_Personal] ON [dbo].[ProyectoAsignacionDia] ([ProyectoPersonalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_ProyectoAsignacionDia_Clave] ON [dbo].[ProyectoAsignacionDia] ([ProyectoId], [EmpleadoId], [Fecha], [RolAsignacionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ProyectoAsignacionDia_LegacyId] ON [dbo].[ProyectoAsignacionDia] ([LegacyId]) WHERE [LegacyId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_ProyectoEtapa_Uid] ON [dbo].[ProyectoEtapa] ([EtapaUid]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_ProyectoEtapa_Version] ON [dbo].[ProyectoEtapa] ([ProyectoId], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ProyectoEtapa_LegacyId] ON [dbo].[ProyectoEtapa] ([LegacyId]) WHERE [LegacyId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE INDEX [IX_ProyectoPersonal_Empleado] ON [dbo].[ProyectoPersonal] ([EmpleadoId]) INCLUDE ([ProyectoId], [FechaInicio], [FechaFin]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_ProyectoPersonal_Numero] ON [dbo].[ProyectoPersonal] ([ProyectoId], [RolAsignacionId], [Numero]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ProyectoPersonal_LegacyId] ON [dbo].[ProyectoPersonal] ([LegacyId]) WHERE [LegacyId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_RolAsignacion_Codigo] ON [dbo].[RolAsignacion] ([Codigo]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_TipoAplicacionNovedad_Codigo] ON [dbo].[TipoAplicacionNovedad] ([Codigo]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_TipoMovimiento_Codigo] ON [dbo].[TipoMovimiento] ([Codigo]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_TipoNovedad_Codigo] ON [dbo].[TipoNovedad] ([Codigo]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Usuario_Email] ON [dbo].[Usuario] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Usuario_EntraObjectId] ON [dbo].[Usuario] ([EntraObjectId]) WHERE [EntraObjectId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_UsuarioDepartamento] ON [dbo].[UsuarioDepartamento] ([UsuarioId], [DepartamentoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    ALTER TABLE [dbo].[CargoInfor] ADD CONSTRAINT [FK_CargoInfor_CreadoPor] FOREIGN KEY ([CreadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    ALTER TABLE [dbo].[CargoInfor] ADD CONSTRAINT [FK_CargoInfor_ModificadoPor] FOREIGN KEY ([ModificadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    ALTER TABLE [dbo].[Compania] ADD CONSTRAINT [FK_Compania_CreadoPor] FOREIGN KEY ([CreadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    ALTER TABLE [dbo].[Compania] ADD CONSTRAINT [FK_Compania_ModificadoPor] FOREIGN KEY ([ModificadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    ALTER TABLE [dbo].[Departamento] ADD CONSTRAINT [FK_Departamento_CreadoPor] FOREIGN KEY ([CreadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    ALTER TABLE [dbo].[Departamento] ADD CONSTRAINT [FK_Departamento_ModificadoPor] FOREIGN KEY ([ModificadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    ALTER TABLE [dbo].[Empleado] ADD CONSTRAINT [FK_Empleado_CreadoPor] FOREIGN KEY ([CreadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    ALTER TABLE [dbo].[Empleado] ADD CONSTRAINT [FK_Empleado_ModificadoPor] FOREIGN KEY ([ModificadoPorId]) REFERENCES [dbo].[Usuario] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161754_Inicial'
)
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260930161754_Inicial', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161822_Vistas'
)
BEGIN
    EXEC(N'CREATE OR ALTER VIEW dbo.vwProyectoResumen
    AS
    SELECT
        p.Id,
        p.Uid,
        p.Codigo,
        p.NombreVisual,
        gp.Codigo            AS Grupo,
        p.FechaInicio,
        p.FechaFin,
        ep.Codigo            AS Estado,
        p.PropietarioUsuarioId,
        p.DepartamentoId,
        p.HoraEntrada,
        p.HoraSalida,
        p.SalidaAlmuerzo,
        p.RegresoAlmuerzo,
        resp.NombreCompleto  AS ResponsableNombre,
        bk.Nombres           AS BacksNombres,
        act.ActividadCodigo,
        act.ActividadDescripcion,
        p.FechaCreacion
    FROM dbo.Proyecto p
    JOIN dbo.EstadoProyecto ep ON ep.Id = p.EstadoProyectoId
    JOIN dbo.GrupoProyecto  gp ON gp.Id = p.GrupoProyectoId
    OUTER APPLY (
        SELECT TOP (1) e.NombreCompleto
        FROM dbo.ProyectoPersonal pp
        JOIN dbo.Empleado e ON e.Id = pp.EmpleadoId
        WHERE pp.ProyectoId = p.Id AND pp.RolAsignacionId = 1 AND pp.EsPrincipalInicial = 1
        ORDER BY pp.FechaFin DESC
    ) resp
    OUTER APPLY (
        SELECT STRING_AGG(x.NombreCompleto, N'' | '') AS Nombres
        FROM (
            SELECT DISTINCT e.NombreCompleto
            FROM dbo.ProyectoPersonal pp
            JOIN dbo.Empleado e ON e.Id = pp.EmpleadoId
            WHERE pp.ProyectoId = p.Id AND pp.RolAsignacionId = 2
        ) x
    ) bk
    OUTER APPLY (
        SELECT TOP (1) a.ActividadCodigo, a.ActividadDescripcion
        FROM dbo.ProyectoActividad a
        WHERE a.ProyectoId = p.Id
        ORDER BY
            CASE WHEN CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), ''-05:00'') AS DATE)
                      BETWEEN a.FechaInicio AND a.FechaFin THEN 0 ELSE 1 END,
            a.Version DESC
    ) act
    WHERE p.Eliminado = 0;');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161822_Vistas'
)
BEGIN
    EXEC(N'CREATE OR ALTER VIEW dbo.vwNovedadDiaVigente
    AS
    SELECT
        nd.Fecha,
        n.Id                AS NovedadId,
        tan.Codigo          AS TipoAplicacion,
        tn.Codigo           AS TipoNovedad,
        tn.Nombre           AS TipoNovedadNombre,
        tn.SiglaCronograma,
        tn.ColorHex,
        tn.EstadoReporte,
        tn.ObservacionReporte,
        n.EmpleadoId,
        n.ProyectoId,
        o.Codigo            AS Origen
    FROM dbo.NovedadDia nd
    JOIN dbo.Novedad n                 ON n.Id   = nd.NovedadId AND n.Anulada = 0
    JOIN dbo.TipoNovedad tn            ON tn.Id  = n.TipoNovedadId
    JOIN dbo.TipoAplicacionNovedad tan ON tan.Id = n.TipoAplicacionNovedadId
    JOIN dbo.OrigenNovedad o           ON o.Id   = n.OrigenNovedadId;');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260930161822_Vistas'
)
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260930161822_Vistas', N'10.0.12');
END;

COMMIT;
GO

