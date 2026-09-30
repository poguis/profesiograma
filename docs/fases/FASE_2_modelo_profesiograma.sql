/* =====================================================================
   PROFESIOGRAMA — Módulo Proyectos
   Script de creación del modelo relacional (esquema dbo)
   Versión   : Fase 2 — diseño objetivo
   Requisito : SQL Server 2017+ (STRING_AGG, ISJSON)  [PENDIENTE confirmar versión]
   Uso       : ejecutar sobre una base VACÍA de pruebas.
               En desarrollo, EF Core (code-first) debe generar un esquema
               equivalente; este script es la referencia de diseño.
   Convención: tablas PascalCase en singular, PK "Id", FK "<Entidad>Id",
               fechas-solo-fecha = DATE, marcas de tiempo = DATETIME2(0) en UTC.
   ===================================================================== */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO
 
BEGIN TRANSACTION;
 
/* ---------------------------------------------------------------------
   1. SEGURIDAD Y CONFIGURACIÓN
   --------------------------------------------------------------------- */
 
CREATE TABLE dbo.Usuario (
    Id                 INT IDENTITY(1,1)  NOT NULL,
    EntraObjectId      UNIQUEIDENTIFIER   NULL,          -- claim "oid"; se completa en el primer login
    Email              NVARCHAR(256)      NOT NULL,      -- UPN / correo corporativo en minúsculas
    NombreMostrar      NVARCHAR(200)      NOT NULL,
    EmpleadoId         INT                NULL,          -- vínculo con ERP (FK se agrega luego)
    EsSistema          BIT                NOT NULL CONSTRAINT DF_Usuario_EsSistema DEFAULT (0),
    Activo             BIT                NOT NULL CONSTRAINT DF_Usuario_Activo DEFAULT (1),
    CreadoPorId        INT                NOT NULL,
    FechaCreacion      DATETIME2(0)       NOT NULL CONSTRAINT DF_Usuario_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    ModificadoPorId    INT                NULL,
    FechaModificacion  DATETIME2(0)       NULL,
    CONSTRAINT PK_Usuario PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Usuario_Email UNIQUE (Email),
    CONSTRAINT FK_Usuario_CreadoPor    FOREIGN KEY (CreadoPorId)     REFERENCES dbo.Usuario(Id),
    CONSTRAINT FK_Usuario_ModificadoPor FOREIGN KEY (ModificadoPorId) REFERENCES dbo.Usuario(Id)
);
CREATE UNIQUE INDEX UX_Usuario_EntraObjectId ON dbo.Usuario(EntraObjectId) WHERE EntraObjectId IS NOT NULL;
 
/* Usuario técnico para cargas, jobs y auditoría del sistema */
SET IDENTITY_INSERT dbo.Usuario ON;
INSERT INTO dbo.Usuario (Id, Email, NombreMostrar, EsSistema, CreadoPorId)
VALUES (1, N'sistema@profesiograma.local', N'SISTEMA', 1, 1);
SET IDENTITY_INSERT dbo.Usuario OFF;
 
CREATE TABLE dbo.Empleado (   -- caché de la API EvolutionEmployee (solo campos necesarios, sin datos sensibles)
    Id                  INT IDENTITY(1,1) NOT NULL,
    CodigoEkon          VARCHAR(20)       NOT NULL,      -- codPersona
    Cedula              VARCHAR(20)       NULL,
    NombreCompleto      NVARCHAR(200)     NOT NULL,
    Apellidos           NVARCHAR(120)     NULL,
    Nombres             NVARCHAR(120)     NULL,
    CorreoEmpresa       NVARCHAR(256)     NULL,          -- mailEmpresa
    CodEmpresa          VARCHAR(10)       NULL,
    Empresa             NVARCHAR(200)     NULL,
    CodPuesto           VARCHAR(10)       NULL,
    Puesto              NVARCHAR(200)     NULL,          -- equivale a "Cargo"
    CodDepartamento     VARCHAR(10)       NULL,
    Departamento        NVARCHAR(200)     NULL,
    CodUnidad           VARCHAR(10)       NULL,
    Unidad              NVARCHAR(200)     NULL,
    CodArea             VARCHAR(10)       NULL,
    Area                NVARCHAR(200)     NULL,
    CodSeccion          VARCHAR(10)       NULL,
    Seccion             NVARCHAR(200)     NULL,
    FamiliaPuesto       NVARCHAR(100)     NULL,          -- "ADMINISTRATIVO", ...
    EstadoErp           CHAR(1)           NULL,          -- "A" = activo
    EsOrigenLegado      BIT               NOT NULL CONSTRAINT DF_Empleado_EsOrigenLegado DEFAULT (0),  -- creado en migración, no hallado en ERP
    FechaSincronizacion DATETIME2(0)      NULL,
    CreadoPorId         INT               NOT NULL,
    FechaCreacion       DATETIME2(0)      NOT NULL CONSTRAINT DF_Empleado_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    ModificadoPorId     INT               NULL,
    FechaModificacion   DATETIME2(0)      NULL,
    CONSTRAINT PK_Empleado PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Empleado_CodigoEkon UNIQUE (CodigoEkon),
    CONSTRAINT FK_Empleado_CreadoPor     FOREIGN KEY (CreadoPorId)     REFERENCES dbo.Usuario(Id),
    CONSTRAINT FK_Empleado_ModificadoPor FOREIGN KEY (ModificadoPorId) REFERENCES dbo.Usuario(Id)
);
CREATE INDEX IX_Empleado_Cedula         ON dbo.Empleado(Cedula);
CREATE INDEX IX_Empleado_CorreoEmpresa  ON dbo.Empleado(CorreoEmpresa);
CREATE INDEX IX_Empleado_Busqueda       ON dbo.Empleado(EstadoErp, FamiliaPuesto) INCLUDE (NombreCompleto, CodigoEkon, Departamento, Unidad, Puesto);
 
ALTER TABLE dbo.Usuario ADD CONSTRAINT FK_Usuario_Empleado FOREIGN KEY (EmpleadoId) REFERENCES dbo.Empleado(Id);
 
CREATE TABLE dbo.Departamento (   -- reemplaza las claves del JSON "PermisosGenerales" (fila 666)
    Id                 INT IDENTITY(1,1) NOT NULL,
    Nombre             NVARCHAR(200)     NOT NULL,       -- nombre exacto usado en ERP / JSON 666
    NombreCorto        NVARCHAR(60)      NOT NULL,       -- etiqueta de UI
    Tipo               VARCHAR(12)       NOT NULL,       -- DEPARTAMENTO | UNIDAD
    CodigoErp          VARCHAR(10)       NULL,           -- codDepartamento / codUnidad  [PENDIENTE]
    Orden              SMALLINT          NOT NULL CONSTRAINT DF_Departamento_Orden DEFAULT (0),
    Activo             BIT               NOT NULL CONSTRAINT DF_Departamento_Activo DEFAULT (1),
    CreadoPorId        INT               NOT NULL,
    FechaCreacion      DATETIME2(0)      NOT NULL CONSTRAINT DF_Departamento_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    ModificadoPorId    INT               NULL,
    FechaModificacion  DATETIME2(0)      NULL,
    CONSTRAINT PK_Departamento PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Departamento_Nombre UNIQUE (Nombre),
    CONSTRAINT CK_Departamento_Tipo CHECK (Tipo IN ('DEPARTAMENTO','UNIDAD')),
    CONSTRAINT FK_Departamento_CreadoPor     FOREIGN KEY (CreadoPorId)     REFERENCES dbo.Usuario(Id),
    CONSTRAINT FK_Departamento_ModificadoPor FOREIGN KEY (ModificadoPorId) REFERENCES dbo.Usuario(Id)
);
 
CREATE TABLE dbo.UsuarioDepartamento (   -- usuarios autorizados por departamento (antes: JSON 666)
    Id                 INT IDENTITY(1,1) NOT NULL,
    UsuarioId          INT               NOT NULL,
    DepartamentoId     INT               NOT NULL,
    Notificado         BIT               NOT NULL CONSTRAINT DF_UsuarioDepartamento_Notificado DEFAULT (0),
    FechaNotificacion  DATETIME2(0)      NULL,
    Activo             BIT               NOT NULL CONSTRAINT DF_UsuarioDepartamento_Activo DEFAULT (1),
    CreadoPorId        INT               NOT NULL,
    FechaCreacion      DATETIME2(0)      NOT NULL CONSTRAINT DF_UsuarioDepartamento_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    ModificadoPorId    INT               NULL,
    FechaModificacion  DATETIME2(0)      NULL,
    CONSTRAINT PK_UsuarioDepartamento PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_UsuarioDepartamento UNIQUE (UsuarioId, DepartamentoId),
    CONSTRAINT FK_UsuarioDepartamento_Usuario      FOREIGN KEY (UsuarioId)      REFERENCES dbo.Usuario(Id),
    CONSTRAINT FK_UsuarioDepartamento_Departamento FOREIGN KEY (DepartamentoId) REFERENCES dbo.Departamento(Id),
    CONSTRAINT FK_UsuarioDepartamento_CreadoPor     FOREIGN KEY (CreadoPorId)     REFERENCES dbo.Usuario(Id),
    CONSTRAINT FK_UsuarioDepartamento_ModificadoPor FOREIGN KEY (ModificadoPorId) REFERENCES dbo.Usuario(Id)
);
 
CREATE TABLE dbo.Parametro (
    Id                 INT IDENTITY(1,1) NOT NULL,
    Clave              VARCHAR(60)       NOT NULL,
    Valor              NVARCHAR(400)     NOT NULL,
    TipoDato           VARCHAR(10)       NOT NULL,
    Descripcion        NVARCHAR(300)     NULL,
    CreadoPorId        INT               NOT NULL,
    FechaCreacion      DATETIME2(0)      NOT NULL CONSTRAINT DF_Parametro_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    ModificadoPorId    INT               NULL,
    FechaModificacion  DATETIME2(0)      NULL,
    CONSTRAINT PK_Parametro PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Parametro_Clave UNIQUE (Clave),
    CONSTRAINT CK_Parametro_TipoDato CHECK (TipoDato IN ('INT','BOOL','TEXT','LIST')),
    CONSTRAINT FK_Parametro_CreadoPor     FOREIGN KEY (CreadoPorId)     REFERENCES dbo.Usuario(Id),
    CONSTRAINT FK_Parametro_ModificadoPor FOREIGN KEY (ModificadoPorId) REFERENCES dbo.Usuario(Id)
);
 
/* ---------------------------------------------------------------------
   2. CATÁLOGOS (Id fijo, Codigo estable usado por el código)
   --------------------------------------------------------------------- */
 
CREATE TABLE dbo.EstadoProyecto (
    Id TINYINT NOT NULL, Codigo VARCHAR(30) NOT NULL, Nombre NVARCHAR(100) NOT NULL,
    EsVigente BIT NOT NULL,                     -- participa en detección de cruces
    Orden TINYINT NOT NULL, Activo BIT NOT NULL CONSTRAINT DF_EstadoProyecto_Activo DEFAULT (1),
    CreadoPorId INT NOT NULL, FechaCreacion DATETIME2(0) NOT NULL CONSTRAINT DF_EstadoProyecto_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    ModificadoPorId INT NULL, FechaModificacion DATETIME2(0) NULL,
    CONSTRAINT PK_EstadoProyecto PRIMARY KEY (Id),
    CONSTRAINT UQ_EstadoProyecto_Codigo UNIQUE (Codigo),
    CONSTRAINT FK_EstadoProyecto_CreadoPor FOREIGN KEY (CreadoPorId) REFERENCES dbo.Usuario(Id),
    CONSTRAINT FK_EstadoProyecto_ModificadoPor FOREIGN KEY (ModificadoPorId) REFERENCES dbo.Usuario(Id)
);
 
CREATE TABLE dbo.TipoMovimiento (
    Id TINYINT NOT NULL, Codigo VARCHAR(30) NOT NULL, Nombre NVARCHAR(100) NOT NULL,
    Orden TINYINT NOT NULL, Activo BIT NOT NULL CONSTRAINT DF_TipoMovimiento_Activo DEFAULT (1),
    CreadoPorId INT NOT NULL, FechaCreacion DATETIME2(0) NOT NULL CONSTRAINT DF_TipoMovimiento_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    ModificadoPorId INT NULL, FechaModificacion DATETIME2(0) NULL,
    CONSTRAINT PK_TipoMovimiento PRIMARY KEY (Id),
    CONSTRAINT UQ_TipoMovimiento_Codigo UNIQUE (Codigo),
    CONSTRAINT FK_TipoMovimiento_CreadoPor FOREIGN KEY (CreadoPorId) REFERENCES dbo.Usuario(Id),
    CONSTRAINT FK_TipoMovimiento_ModificadoPor FOREIGN KEY (ModificadoPorId) REFERENCES dbo.Usuario(Id)
);
 
CREATE TABLE dbo.GrupoProyecto (
    Id TINYINT NOT NULL, Codigo VARCHAR(30) NOT NULL, Nombre NVARCHAR(100) NOT NULL,
    RequiereProyectoErp BIT NOT NULL, RequiereDimension BIT NOT NULL,
    Orden TINYINT NOT NULL, Activo BIT NOT NULL CONSTRAINT DF_GrupoProyecto_Activo DEFAULT (1),
    CreadoPorId INT NOT NULL, FechaCreacion DATETIME2(0) NOT NULL CONSTRAINT DF_GrupoProyecto_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    ModificadoPorId INT NULL, FechaModificacion DATETIME2(0) NULL,
    CONSTRAINT PK_GrupoProyecto PRIMARY KEY (Id),
    CONSTRAINT UQ_GrupoProyecto_Codigo UNIQUE (Codigo),
    CONSTRAINT CK_GrupoProyecto_Requisito CHECK (RequiereProyectoErp = 1 OR RequiereDimension = 1),
    CONSTRAINT FK_GrupoProyecto_CreadoPor FOREIGN KEY (CreadoPorId) REFERENCES dbo.Usuario(Id),
    CONSTRAINT FK_GrupoProyecto_ModificadoPor FOREIGN KEY (ModificadoPorId) REFERENCES dbo.Usuario(Id)
);
 
CREATE TABLE dbo.Jornada (
    Id TINYINT NOT NULL, Codigo VARCHAR(30) NOT NULL, Nombre NVARCHAR(100) NOT NULL,
    DiasTrabajo TINYINT NOT NULL, DiasDescanso TINYINT NOT NULL,
    Orden TINYINT NOT NULL, Activo BIT NOT NULL CONSTRAINT DF_Jornada_Activo DEFAULT (1),
    CreadoPorId INT NOT NULL, FechaCreacion DATETIME2(0) NOT NULL CONSTRAINT DF_Jornada_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    ModificadoPorId INT NULL, FechaModificacion DATETIME2(0) NULL,
    CONSTRAINT PK_Jornada PRIMARY KEY (Id),
    CONSTRAINT UQ_Jornada_Codigo UNIQUE (Codigo),
    CONSTRAINT CK_Jornada_Dias CHECK (DiasTrabajo > 0 AND DiasDescanso >= 0),
    CONSTRAINT FK_Jornada_CreadoPor FOREIGN KEY (CreadoPorId) REFERENCES dbo.Usuario(Id),
    CONSTRAINT FK_Jornada_ModificadoPor FOREIGN KEY (ModificadoPorId) REFERENCES dbo.Usuario(Id)
);
 
CREATE TABLE dbo.RolAsignacion (
    Id TINYINT NOT NULL, Codigo VARCHAR(30) NOT NULL, Nombre NVARCHAR(100) NOT NULL,
    EsDescanso BIT NOT NULL,
    Orden TINYINT NOT NULL, Activo BIT NOT NULL CONSTRAINT DF_RolAsignacion_Activo DEFAULT (1),
    CreadoPorId INT NOT NULL, FechaCreacion DATETIME2(0) NOT NULL CONSTRAINT DF_RolAsignacion_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    ModificadoPorId INT NULL, FechaModificacion DATETIME2(0) NULL,
    CONSTRAINT PK_RolAsignacion PRIMARY KEY (Id),
    CONSTRAINT UQ_RolAsignacion_Codigo UNIQUE (Codigo),
    CONSTRAINT FK_RolAsignacion_CreadoPor FOREIGN KEY (CreadoPorId) REFERENCES dbo.Usuario(Id),
    CONSTRAINT FK_RolAsignacion_ModificadoPor FOREIGN KEY (ModificadoPorId) REFERENCES dbo.Usuario(Id)
);
 
CREATE TABLE dbo.TipoAplicacionNovedad (
    Id TINYINT NOT NULL, Codigo VARCHAR(30) NOT NULL, Nombre NVARCHAR(100) NOT NULL,
    Orden TINYINT NOT NULL, Activo BIT NOT NULL CONSTRAINT DF_TipoAplicacionNovedad_Activo DEFAULT (1),
    CreadoPorId INT NOT NULL, FechaCreacion DATETIME2(0) NOT NULL CONSTRAINT DF_TipoAplicacionNovedad_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    ModificadoPorId INT NULL, FechaModificacion DATETIME2(0) NULL,
    CONSTRAINT PK_TipoAplicacionNovedad PRIMARY KEY (Id),
    CONSTRAINT UQ_TipoAplicacionNovedad_Codigo UNIQUE (Codigo),
    CONSTRAINT FK_TipoAplicacionNovedad_CreadoPor FOREIGN KEY (CreadoPorId) REFERENCES dbo.Usuario(Id),
    CONSTRAINT FK_TipoAplicacionNovedad_ModificadoPor FOREIGN KEY (ModificadoPorId) REFERENCES dbo.Usuario(Id)
);
 
CREATE TABLE dbo.OrigenNovedad (
    Id TINYINT NOT NULL, Codigo VARCHAR(30) NOT NULL, Nombre NVARCHAR(100) NOT NULL,
    EsEditable BIT NOT NULL,                    -- las novedades externas son de solo lectura
    Orden TINYINT NOT NULL, Activo BIT NOT NULL CONSTRAINT DF_OrigenNovedad_Activo DEFAULT (1),
    CreadoPorId INT NOT NULL, FechaCreacion DATETIME2(0) NOT NULL CONSTRAINT DF_OrigenNovedad_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    ModificadoPorId INT NULL, FechaModificacion DATETIME2(0) NULL,
    CONSTRAINT PK_OrigenNovedad PRIMARY KEY (Id),
    CONSTRAINT UQ_OrigenNovedad_Codigo UNIQUE (Codigo),
    CONSTRAINT FK_OrigenNovedad_CreadoPor FOREIGN KEY (CreadoPorId) REFERENCES dbo.Usuario(Id),
    CONSTRAINT FK_OrigenNovedad_ModificadoPor FOREIGN KEY (ModificadoPorId) REFERENCES dbo.Usuario(Id)
);
 
CREATE TABLE dbo.TipoNovedad (
    Id                  TINYINT       NOT NULL,
    Codigo              VARCHAR(30)   NOT NULL,
    Nombre              NVARCHAR(100) NOT NULL,
    Sigla               VARCHAR(10)   NOT NULL,     -- sigla original (CD, D, FER, PTNDAD, P, V, PM)
    SiglaCronograma     VARCHAR(4)    NOT NULL,     -- texto de la celda (CD, F, V, PT, P, PM)
    ColorHex            CHAR(7)       NOT NULL,
    CategoriaId         INT           NOT NULL,     -- "CategoriaId" del JSON (5 = Asistencia Libre)
    CategoriaDescripcion NVARCHAR(100) NOT NULL,
    EstadoReporte       VARCHAR(20)   NOT NULL,     -- ESTADO en el Excel (LIBRE)
    ObservacionReporte  NVARCHAR(60)  NOT NULL,     -- OBSERVACION en el Excel
    AplicaPersona       BIT           NOT NULL,
    AplicaProyecto      BIT           NOT NULL,
    AplicaGeneral       BIT           NOT NULL,
    SeleccionableManual BIT           NOT NULL,     -- 0 = solo por integración (PERMISO MEDICO) o sistema (DESCANSO)
    Orden               TINYINT       NOT NULL,
    Activo              BIT           NOT NULL CONSTRAINT DF_TipoNovedad_Activo DEFAULT (1),
    CreadoPorId INT NOT NULL, FechaCreacion DATETIME2(0) NOT NULL CONSTRAINT DF_TipoNovedad_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    ModificadoPorId INT NULL, FechaModificacion DATETIME2(0) NULL,
    CONSTRAINT PK_TipoNovedad PRIMARY KEY (Id),
    CONSTRAINT UQ_TipoNovedad_Codigo UNIQUE (Codigo),
    CONSTRAINT CK_TipoNovedad_ColorHex CHECK (ColorHex LIKE '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]'),
    CONSTRAINT FK_TipoNovedad_CreadoPor FOREIGN KEY (CreadoPorId) REFERENCES dbo.Usuario(Id),
    CONSTRAINT FK_TipoNovedad_ModificadoPor FOREIGN KEY (ModificadoPorId) REFERENCES dbo.Usuario(Id)
);
 
CREATE TABLE dbo.CargoInfor (   -- mapeo cargo → código Infor (CRUD + importación desde Excel)
    Id                 INT IDENTITY(1,1) NOT NULL,
    Cargo              NVARCHAR(200)     NOT NULL,     -- comparado en mayúsculas y sin espacios extremos
    CodigoInfor        VARCHAR(20)       NULL,          -- se completa desde la interfaz
    Activo             BIT               NOT NULL CONSTRAINT DF_CargoInfor_Activo DEFAULT (1),
    CreadoPorId        INT               NOT NULL,
    FechaCreacion      DATETIME2(0)      NOT NULL CONSTRAINT DF_CargoInfor_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    ModificadoPorId    INT               NULL,
    FechaModificacion  DATETIME2(0)      NULL,
    CONSTRAINT PK_CargoInfor PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_CargoInfor_Cargo UNIQUE (Cargo),
    CONSTRAINT FK_CargoInfor_CreadoPor     FOREIGN KEY (CreadoPorId)     REFERENCES dbo.Usuario(Id),
    CONSTRAINT FK_CargoInfor_ModificadoPor FOREIGN KEY (ModificadoPorId) REFERENCES dbo.Usuario(Id)
);
 
CREATE TABLE dbo.Compania (   -- caché de compañías ERP (Id = companyId del ERP)
    Id                 INT               NOT NULL,
    Nombre             NVARCHAR(300)     NOT NULL,
    NombreComercial    NVARCHAR(300)     NULL,
    Ruc                VARCHAR(13)       NULL,
    CreadoPorId        INT               NOT NULL,
    FechaCreacion      DATETIME2(0)      NOT NULL CONSTRAINT DF_Compania_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    ModificadoPorId    INT               NULL,
    FechaModificacion  DATETIME2(0)      NULL,
    CONSTRAINT PK_Compania PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Compania_CreadoPor     FOREIGN KEY (CreadoPorId)     REFERENCES dbo.Usuario(Id),
    CONSTRAINT FK_Compania_ModificadoPor FOREIGN KEY (ModificadoPorId) REFERENCES dbo.Usuario(Id)
);
 
/* ---------------------------------------------------------------------
   3. PROYECTOS
   --------------------------------------------------------------------- */
 
CREATE TABLE dbo.Proyecto (   -- antes: SIG PROYECTOS (columnas planas)
    Id                    INT IDENTITY(1,1)  NOT NULL,
    Uid                   UNIQUEIDENTIFIER   NOT NULL CONSTRAINT DF_Proyecto_Uid DEFAULT (NEWID()),  -- PROYECTO_UID
    Codigo                VARCHAR(30)        NOT NULL,   -- PRY-yyyymmdd-xxxxxx
    NombreVisual          NVARCHAR(300)      NOT NULL,   -- NOMBRE_PROYECTO
    GrupoProyectoId       TINYINT            NOT NULL,
    CompaniaId            INT                NOT NULL,
    ProyectoErpId         VARCHAR(30)        NULL,       -- PROJECT_ID_EKON
    ProyectoErpNombre     NVARCHAR(300)      NULL,
    ProyectoErpEstado     NVARCHAR(30)       NULL,
    DimensionUegpId       VARCHAR(30)        NULL,
    DimensionDescripcion  NVARCHAR(300)      NULL,
    FechaInicio           DATE               NOT NULL,
    FechaFin              DATE               NOT NULL,
    EstadoProyectoId      TINYINT            NOT NULL,
    HorarioCodigo         INT                NULL,
    HorarioDescripcion    NVARCHAR(200)      NULL,
    HoraEntrada           TIME(0)            NULL,       -- "0700" → 07:00
    HoraSalida            TIME(0)            NULL,
    HorasJornadaMin       SMALLINT           NULL,       -- "1100" → 660
    HorasTrabajadasMin    SMALLINT           NULL,       -- "1000" → 600
    TipoHorario           VARCHAR(5)         NULL,
    SalidaAlmuerzo        TIME(0)            NULL,
    RegresoAlmuerzo       TIME(0)            NULL,
    DepartamentoId        INT                NULL,       -- depto bajo el que se registró (visibilidad futura)
    PropietarioUsuarioId  INT                NOT NULL,   -- dueño actual (por defecto el creador; reasignable)
    Eliminado             BIT                NOT NULL CONSTRAINT DF_Proyecto_Eliminado DEFAULT (0),
    LegacyId              INT                NULL,       -- ID de SharePoint
    RowVer                ROWVERSION         NOT NULL,   -- concurrencia optimista
    CreadoPorId           INT                NOT NULL,
    FechaCreacion         DATETIME2(0)       NOT NULL CONSTRAINT DF_Proyecto_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    ModificadoPorId       INT                NULL,
    FechaModificacion     DATETIME2(0)       NULL,
    CONSTRAINT PK_Proyecto PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Proyecto_Uid    UNIQUE (Uid),
    CONSTRAINT UQ_Proyecto_Codigo UNIQUE (Codigo),
    CONSTRAINT CK_Proyecto_Fechas   CHECK (FechaFin >= FechaInicio),
    CONSTRAINT CK_Proyecto_Almuerzo CHECK (SalidaAlmuerzo IS NULL OR RegresoAlmuerzo IS NULL OR RegresoAlmuerzo > SalidaAlmuerzo),
    CONSTRAINT CK_Proyecto_Origen   CHECK (ProyectoErpId IS NOT NULL OR DimensionUegpId IS NOT NULL),
    CONSTRAINT FK_Proyecto_GrupoProyecto  FOREIGN KEY (GrupoProyectoId)      REFERENCES dbo.GrupoProyecto(Id),
    CONSTRAINT FK_Proyecto_Compania       FOREIGN KEY (CompaniaId)           REFERENCES dbo.Compania(Id),
    CONSTRAINT FK_Proyecto_EstadoProyecto FOREIGN KEY (EstadoProyectoId)     REFERENCES dbo.EstadoProyecto(Id),
    CONSTRAINT FK_Proyecto_Departamento   FOREIGN KEY (DepartamentoId)       REFERENCES dbo.Departamento(Id),
    CONSTRAINT FK_Proyecto_Propietario    FOREIGN KEY (PropietarioUsuarioId) REFERENCES dbo.Usuario(Id),
    CONSTRAINT FK_Proyecto_CreadoPor      FOREIGN KEY (CreadoPorId)          REFERENCES dbo.Usuario(Id),
    CONSTRAINT FK_Proyecto_ModificadoPor  FOREIGN KEY (ModificadoPorId)      REFERENCES dbo.Usuario(Id)
);
CREATE INDEX IX_Proyecto_Estado       ON dbo.Proyecto(EstadoProyectoId) INCLUDE (Codigo, NombreVisual, FechaInicio, FechaFin) WHERE Eliminado = 0;
CREATE INDEX IX_Proyecto_Propietario  ON dbo.Proyecto(PropietarioUsuarioId) WHERE Eliminado = 0;
CREATE INDEX IX_Proyecto_Departamento ON dbo.Proyecto(DepartamentoId) WHERE Eliminado = 0;
CREATE INDEX IX_Proyecto_Nombre       ON dbo.Proyecto(NombreVisual);
CREATE UNIQUE INDEX UX_Proyecto_LegacyId ON dbo.Proyecto(LegacyId) WHERE LegacyId IS NOT NULL;
 
CREATE TABLE dbo.ProyectoPersonal (   -- antes: SIG_ASIGNACIONES_PERSONAL
    Id                     INT IDENTITY(1,1) NOT NULL,
    ProyectoId             INT               NOT NULL,
    RolAsignacionId        TINYINT           NOT NULL,   -- 1 PRINCIPAL | 2 BACK
    Numero                 SMALLINT          NOT NULL,   -- PERSONA_ID (PrincipalId / BackId dentro del proyecto)
    EmpleadoId             INT               NOT NULL,
    CargoAsignado          NVARCHAR(200)     NULL,       -- cargo vigente al asignar (para CARGO INFOR)
    FechaInicio            DATE              NOT NULL,
    FechaFin               DATE              NOT NULL,
    JornadaId              TINYINT           NULL,       -- obligatorio para PRINCIPAL
    DiasTrabajo            TINYINT           NULL,       -- copia de la jornada al asignar
    DiasDescanso           TINYINT           NOT NULL CONSTRAINT DF_ProyectoPersonal_DiasDescanso DEFAULT (0),
    EsPrincipalInicial     BIT               NOT NULL CONSTRAINT DF_ProyectoPersonal_EsPrincipalInicial DEFAULT (0),
    TipoRegistro           VARCHAR(10)       NOT NULL CONSTRAINT DF_ProyectoPersonal_TipoRegistro DEFAULT ('JORNADA'),
    PrincipalRelacionadoId INT               NULL,       -- PRINCIPAL_ID_RELACIONADO (hoy siempre vacío)
    Observacion            NVARCHAR(500)     NULL,
    LegacyId               INT               NULL,
    CreadoPorId            INT               NOT NULL,
    FechaCreacion          DATETIME2(0)      NOT NULL CONSTRAINT DF_ProyectoPersonal_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    ModificadoPorId        INT               NULL,
    FechaModificacion      DATETIME2(0)      NULL,
    CONSTRAINT PK_ProyectoPersonal PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_ProyectoPersonal_Numero UNIQUE (ProyectoId, RolAsignacionId, Numero),
    CONSTRAINT UQ_ProyectoPersonal_Clave  UNIQUE (Id, ProyectoId, EmpleadoId),       -- soporte de FK compuesta
    CONSTRAINT CK_ProyectoPersonal_Rol    CHECK (RolAsignacionId IN (1, 2)),
    CONSTRAINT CK_ProyectoPersonal_Fechas CHECK (FechaFin >= FechaInicio),
    CONSTRAINT CK_ProyectoPersonal_TipoRegistro CHECK (TipoRegistro IN ('JORNADA','DESCANSO')),
    CONSTRAINT CK_ProyectoPersonal_Jornada CHECK (RolAsignacionId <> 1 OR (JornadaId IS NOT NULL AND DiasTrabajo > 0)),
    CONSTRAINT CK_ProyectoPersonal_Inicial CHECK (EsPrincipalInicial = 0 OR RolAsignacionId = 1),
    CONSTRAINT FK_ProyectoPersonal_Proyecto   FOREIGN KEY (ProyectoId)             REFERENCES dbo.Proyecto(Id),
    CONSTRAINT FK_ProyectoPersonal_Rol        FOREIGN KEY (RolAsignacionId)        REFERENCES dbo.RolAsignacion(Id),
    CONSTRAINT FK_ProyectoPersonal_Empleado   FOREIGN KEY (EmpleadoId)             REFERENCES dbo.Empleado(Id),
    CONSTRAINT FK_ProyectoPersonal_Jornada    FOREIGN KEY (JornadaId)              REFERENCES dbo.Jornada(Id),
    CONSTRAINT FK_ProyectoPersonal_Principal  FOREIGN KEY (PrincipalRelacionadoId) REFERENCES dbo.ProyectoPersonal(Id),
    CONSTRAINT FK_ProyectoPersonal_CreadoPor     FOREIGN KEY (CreadoPorId)     REFERENCES dbo.Usuario(Id),
    CONSTRAINT FK_ProyectoPersonal_ModificadoPor FOREIGN KEY (ModificadoPorId) REFERENCES dbo.Usuario(Id)
);
CREATE INDEX IX_ProyectoPersonal_Empleado ON dbo.ProyectoPersonal(EmpleadoId) INCLUDE (ProyectoId, FechaInicio, FechaFin);
CREATE UNIQUE INDEX UX_ProyectoPersonal_LegacyId ON dbo.ProyectoPersonal(LegacyId) WHERE LegacyId IS NOT NULL;
 
CREATE TABLE dbo.ProyectoAsignacionDia (   -- antes: SIG_ASIGNACIONES_DETALLE
    Id                  BIGINT IDENTITY(1,1) NOT NULL,
    ProyectoId          INT                  NOT NULL,
    ProyectoPersonalId  INT                  NOT NULL,
    EmpleadoId          INT                  NOT NULL,   -- redundante controlado por FK compuesta (índice de cruces)
    Fecha               DATE                 NOT NULL,
    RolAsignacionId     TINYINT              NOT NULL,   -- PRINCIPAL | BACK | DESCANSO
    TipoAsignacion      VARCHAR(6)           NOT NULL,   -- AUTO | MANUAL
    Bloque              SMALLINT             NOT NULL,
    LegacyId            INT                  NULL,
    CreadoPorId         INT                  NOT NULL,
    FechaCreacion       DATETIME2(0)         NOT NULL CONSTRAINT DF_ProyectoAsignacionDia_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    ModificadoPorId     INT                  NULL,
    FechaModificacion   DATETIME2(0)         NULL,
    CONSTRAINT PK_ProyectoAsignacionDia PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_ProyectoAsignacionDia_Clave UNIQUE (ProyectoId, EmpleadoId, Fecha, RolAsignacionId),   -- CLAVE original UID|EKON|fecha|ROL
    CONSTRAINT CK_ProyectoAsignacionDia_Tipo  CHECK (TipoAsignacion IN ('AUTO','MANUAL')),
    CONSTRAINT FK_ProyectoAsignacionDia_Personal FOREIGN KEY (ProyectoPersonalId, ProyectoId, EmpleadoId)
        REFERENCES dbo.ProyectoPersonal(Id, ProyectoId, EmpleadoId),
    CONSTRAINT FK_ProyectoAsignacionDia_Proyecto FOREIGN KEY (ProyectoId)      REFERENCES dbo.Proyecto(Id),
    CONSTRAINT FK_ProyectoAsignacionDia_Rol      FOREIGN KEY (RolAsignacionId) REFERENCES dbo.RolAsignacion(Id),
    CONSTRAINT FK_ProyectoAsignacionDia_CreadoPor     FOREIGN KEY (CreadoPorId)     REFERENCES dbo.Usuario(Id),
    CONSTRAINT FK_ProyectoAsignacionDia_ModificadoPor FOREIGN KEY (ModificadoPorId) REFERENCES dbo.Usuario(Id)
);
CREATE INDEX IX_ProyectoAsignacionDia_Cruces     ON dbo.ProyectoAsignacionDia(EmpleadoId, Fecha) INCLUDE (ProyectoId, RolAsignacionId);
CREATE INDEX IX_ProyectoAsignacionDia_Cronograma ON dbo.ProyectoAsignacionDia(Fecha) INCLUDE (ProyectoId, EmpleadoId, RolAsignacionId);
CREATE INDEX IX_ProyectoAsignacionDia_Personal   ON dbo.ProyectoAsignacionDia(ProyectoPersonalId);
CREATE UNIQUE INDEX UX_ProyectoAsignacionDia_LegacyId ON dbo.ProyectoAsignacionDia(LegacyId) WHERE LegacyId IS NOT NULL;
 
CREATE TABLE dbo.ProyectoEtapa (   -- antes: SIG_HISTORIAL
    Id                 INT IDENTITY(1,1) NOT NULL,
    ProyectoId         INT               NOT NULL,
    EtapaUid           UNIQUEIDENTIFIER  NOT NULL CONSTRAINT DF_ProyectoEtapa_EtapaUid DEFAULT (NEWID()),
    Version            INT               NOT NULL,
    TipoMovimientoId   TINYINT           NOT NULL,
    EstadoProyectoId   TINYINT           NOT NULL,
    FechaInicio        DATE              NOT NULL,
    FechaFin           DATE              NOT NULL,
    FechaCorte         DATE              NULL,
    ActividadCodigo    VARCHAR(20)       NULL,
    SnapshotPersonal   NVARCHAR(MAX)     NULL,       -- JSON de auditoría (no se consulta)
    LegacyId           INT               NULL,
    CreadoPorId        INT               NOT NULL,   -- USUARIO/CORREO_REGISTRO
    FechaCreacion      DATETIME2(0)      NOT NULL CONSTRAINT DF_ProyectoEtapa_FechaCreacion DEFAULT (SYSUTCDATETIME()),  -- FECHA_REGISTRO
    ModificadoPorId    INT               NULL,
    FechaModificacion  DATETIME2(0)      NULL,
    CONSTRAINT PK_ProyectoEtapa PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_ProyectoEtapa_Version UNIQUE (ProyectoId, Version),
    CONSTRAINT UQ_ProyectoEtapa_Uid     UNIQUE (EtapaUid),
    CONSTRAINT CK_ProyectoEtapa_Fechas   CHECK (FechaFin >= FechaInicio),
    CONSTRAINT CK_ProyectoEtapa_Snapshot CHECK (SnapshotPersonal IS NULL OR ISJSON(SnapshotPersonal) = 1),
    CONSTRAINT FK_ProyectoEtapa_Proyecto       FOREIGN KEY (ProyectoId)       REFERENCES dbo.Proyecto(Id),
    CONSTRAINT FK_ProyectoEtapa_TipoMovimiento FOREIGN KEY (TipoMovimientoId) REFERENCES dbo.TipoMovimiento(Id),
    CONSTRAINT FK_ProyectoEtapa_Estado         FOREIGN KEY (EstadoProyectoId) REFERENCES dbo.EstadoProyecto(Id),
    CONSTRAINT FK_ProyectoEtapa_CreadoPor      FOREIGN KEY (CreadoPorId)      REFERENCES dbo.Usuario(Id),
    CONSTRAINT FK_ProyectoEtapa_ModificadoPor  FOREIGN KEY (ModificadoPorId)  REFERENCES dbo.Usuario(Id)
);
CREATE UNIQUE INDEX UX_ProyectoEtapa_LegacyId ON dbo.ProyectoEtapa(LegacyId) WHERE LegacyId IS NOT NULL;
 
CREATE TABLE dbo.ProyectoActividad (   -- antes: SIG_HISTORIAL_ACTIVIDADES
    Id                    INT IDENTITY(1,1) NOT NULL,
    ProyectoId            INT               NOT NULL,
    MovimientoUid         UNIQUEIDENTIFIER  NOT NULL CONSTRAINT DF_ProyectoActividad_Uid DEFAULT (NEWID()),
    Version               INT               NOT NULL,
    TipoMovimientoId      TINYINT           NOT NULL,   -- CREACION | CAMBIO_ACTIVIDAD
    FechaInicio           DATE              NOT NULL,
    FechaFin              DATE              NOT NULL,
    ActividadCodigo       VARCHAR(20)       NOT NULL,   -- activityId ERP (p. ej. "93.06")
    ActividadDescripcion  NVARCHAR(300)     NULL,
    ActividadTipo         NVARCHAR(100)     NULL,
    LegacyId              INT               NULL,
    CreadoPorId           INT               NOT NULL,
    FechaCreacion         DATETIME2(0)      NOT NULL CONSTRAINT DF_ProyectoActividad_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    ModificadoPorId       INT               NULL,
    FechaModificacion     DATETIME2(0)      NULL,
    CONSTRAINT PK_ProyectoActividad PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_ProyectoActividad_Version UNIQUE (ProyectoId, Version),
    CONSTRAINT UQ_ProyectoActividad_Uid     UNIQUE (MovimientoUid),
    CONSTRAINT CK_ProyectoActividad_Fechas  CHECK (FechaFin >= FechaInicio),
    CONSTRAINT FK_ProyectoActividad_Proyecto       FOREIGN KEY (ProyectoId)       REFERENCES dbo.Proyecto(Id),
    CONSTRAINT FK_ProyectoActividad_TipoMovimiento FOREIGN KEY (TipoMovimientoId) REFERENCES dbo.TipoMovimiento(Id),
    CONSTRAINT FK_ProyectoActividad_CreadoPor      FOREIGN KEY (CreadoPorId)      REFERENCES dbo.Usuario(Id),
    CONSTRAINT FK_ProyectoActividad_ModificadoPor  FOREIGN KEY (ModificadoPorId)  REFERENCES dbo.Usuario(Id)
);
CREATE INDEX IX_ProyectoActividad_Vigencia ON dbo.ProyectoActividad(ProyectoId, FechaInicio, FechaFin) INCLUDE (ActividadCodigo, Version);
CREATE UNIQUE INDEX UX_ProyectoActividad_LegacyId ON dbo.ProyectoActividad(LegacyId) WHERE LegacyId IS NOT NULL;
 
/* ---------------------------------------------------------------------
   4. NOVEDADES
   --------------------------------------------------------------------- */
 
CREATE TABLE dbo.Novedad (   -- antes: NOVEDADES ASISTENCIA
    Id                       INT IDENTITY(1,1) NOT NULL,
    Uid                      UNIQUEIDENTIFIER  NOT NULL CONSTRAINT DF_Novedad_Uid DEFAULT (NEWID()),   -- NOVEDAD_UID (clave de sincronización)
    Codigo                   VARCHAR(30)       NOT NULL,                                              -- NOV-yyyymmdd-xxxxxx
    TipoAplicacionNovedadId  TINYINT           NOT NULL,
    TipoNovedadId            TINYINT           NOT NULL,
    OrigenNovedadId          TINYINT           NOT NULL,
    EmpleadoId               INT               NULL,       -- solo PERSONA
    ProyectoId               INT               NULL,       -- solo PROYECTO
    FechaInicio              DATE              NOT NULL,
    FechaFin                 DATE              NOT NULL,
    Observacion              NVARCHAR(500)     NULL,
    OrigenArea               NVARCHAR(30)      NULL,       -- "PLANTA" / "CAMPO" (permisos médicos)
    RegistradoPorNombre      NVARCHAR(200)     NULL,       -- registrante en app externa
    RegistradoPorCorreo      NVARCHAR(256)     NULL,
    DetalleOrigenJson        NVARCHAR(MAX)     NULL,       -- JSON original (trazabilidad de migración/sincronización)
    Anulada                  BIT               NOT NULL CONSTRAINT DF_Novedad_Anulada DEFAULT (0),
    FechaAnulacion           DATETIME2(0)      NULL,
    AnuladaPorId             INT               NULL,
    MotivoAnulacion          NVARCHAR(500)     NULL,
    LegacyId                 INT               NULL,
    RowVer                   ROWVERSION        NOT NULL,
    CreadoPorId              INT               NOT NULL,
    FechaCreacion            DATETIME2(0)      NOT NULL CONSTRAINT DF_Novedad_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    ModificadoPorId          INT               NULL,
    FechaModificacion        DATETIME2(0)      NULL,
    CONSTRAINT PK_Novedad PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Novedad_Uid    UNIQUE (Uid),
    CONSTRAINT UQ_Novedad_Codigo UNIQUE (Codigo),
    CONSTRAINT CK_Novedad_Fechas CHECK (FechaFin >= FechaInicio),
    CONSTRAINT CK_Novedad_Aplicacion CHECK (
           (TipoAplicacionNovedadId = 1 AND EmpleadoId IS NOT NULL AND ProyectoId IS NULL)   -- PERSONA
        OR (TipoAplicacionNovedadId = 2 AND ProyectoId IS NOT NULL AND EmpleadoId IS NULL)   -- PROYECTO
        OR (TipoAplicacionNovedadId = 3 AND ProyectoId IS NULL     AND EmpleadoId IS NULL)), -- GENERAL
    CONSTRAINT CK_Novedad_Anulacion CHECK (Anulada = 0 OR (FechaAnulacion IS NOT NULL AND AnuladaPorId IS NOT NULL)),
    CONSTRAINT CK_Novedad_DetalleJson CHECK (DetalleOrigenJson IS NULL OR ISJSON(DetalleOrigenJson) = 1),
    CONSTRAINT FK_Novedad_TipoAplicacion FOREIGN KEY (TipoAplicacionNovedadId) REFERENCES dbo.TipoAplicacionNovedad(Id),
    CONSTRAINT FK_Novedad_TipoNovedad    FOREIGN KEY (TipoNovedadId)           REFERENCES dbo.TipoNovedad(Id),
    CONSTRAINT FK_Novedad_Origen         FOREIGN KEY (OrigenNovedadId)         REFERENCES dbo.OrigenNovedad(Id),
    CONSTRAINT FK_Novedad_Empleado       FOREIGN KEY (EmpleadoId)              REFERENCES dbo.Empleado(Id),
    CONSTRAINT FK_Novedad_Proyecto       FOREIGN KEY (ProyectoId)              REFERENCES dbo.Proyecto(Id),
    CONSTRAINT FK_Novedad_AnuladaPor     FOREIGN KEY (AnuladaPorId)            REFERENCES dbo.Usuario(Id),
    CONSTRAINT FK_Novedad_CreadoPor      FOREIGN KEY (CreadoPorId)             REFERENCES dbo.Usuario(Id),
    CONSTRAINT FK_Novedad_ModificadoPor  FOREIGN KEY (ModificadoPorId)         REFERENCES dbo.Usuario(Id)
);
CREATE INDEX IX_Novedad_Empleado ON dbo.Novedad(EmpleadoId) WHERE EmpleadoId IS NOT NULL AND Anulada = 0;
CREATE INDEX IX_Novedad_Proyecto ON dbo.Novedad(ProyectoId) WHERE ProyectoId IS NOT NULL AND Anulada = 0;
CREATE INDEX IX_Novedad_Rango    ON dbo.Novedad(FechaInicio, FechaFin) INCLUDE (TipoAplicacionNovedadId, TipoNovedadId, EmpleadoId, ProyectoId) WHERE Anulada = 0;
CREATE UNIQUE INDEX UX_Novedad_LegacyId ON dbo.Novedad(LegacyId, OrigenNovedadId) WHERE LegacyId IS NOT NULL;
 
CREATE TABLE dbo.NovedadDia (   -- antes: DETALLE_JSON.Dias[]  (permite quitar días puntuales)
    Id                 BIGINT IDENTITY(1,1) NOT NULL,
    NovedadId          INT                  NOT NULL,
    Fecha              DATE                 NOT NULL,
    CreadoPorId        INT                  NOT NULL,
    FechaCreacion      DATETIME2(0)         NOT NULL CONSTRAINT DF_NovedadDia_FechaCreacion DEFAULT (SYSUTCDATETIME()),
    ModificadoPorId    INT                  NULL,
    FechaModificacion  DATETIME2(0)         NULL,
    CONSTRAINT PK_NovedadDia PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_NovedadDia UNIQUE (NovedadId, Fecha),
    CONSTRAINT FK_NovedadDia_Novedad       FOREIGN KEY (NovedadId)       REFERENCES dbo.Novedad(Id) ON DELETE CASCADE,
    CONSTRAINT FK_NovedadDia_CreadoPor     FOREIGN KEY (CreadoPorId)     REFERENCES dbo.Usuario(Id),
    CONSTRAINT FK_NovedadDia_ModificadoPor FOREIGN KEY (ModificadoPorId) REFERENCES dbo.Usuario(Id)
);
CREATE INDEX IX_NovedadDia_Fecha ON dbo.NovedadDia(Fecha) INCLUDE (NovedadId);
 
/* ---------------------------------------------------------------------
   5. DATOS SEMILLA
   --------------------------------------------------------------------- */
 
INSERT INTO dbo.EstadoProyecto (Id, Codigo, Nombre, EsVigente, Orden, CreadoPorId) VALUES
 (1, 'ACTIVO',     N'Activo',     1, 1, 1),
 (2, 'SUSPENDIDO', N'Suspendido', 1, 2, 1),
 (3, 'INACTIVO',   N'Inactivo',   0, 3, 1),   -- existe en SharePoint; sin transición que lo alcance (C8)
 (4, 'TERMINADO',  N'Terminado',  0, 4, 1);
 
INSERT INTO dbo.TipoMovimiento (Id, Codigo, Nombre, Orden, CreadoPorId) VALUES
 (1, 'CREACION',               N'Creación',                 1, 1),
 (2, 'ACTUALIZACION_PERSONAL', N'Actualización de personal', 2, 1),
 (3, 'SUSPENSION',             N'Suspensión',               3, 1),
 (4, 'CIERRE',                 N'Cierre',                   4, 1),
 (5, 'REACTIVACION',           N'Reactivación',             5, 1),
 (6, 'CAMBIO_ACTIVIDAD',       N'Cambio de actividad',      6, 1),
 (7, 'EDICION_CABECERA',       N'Edición de datos generales', 7, 1);   -- nuevo (horario, fechas, etc.)
 
INSERT INTO dbo.GrupoProyecto (Id, Codigo, Nombre, RequiereProyectoErp, RequiereDimension, Orden, CreadoPorId) VALUES
 (1, 'CAMPO',                    N'CAMPO',                    1, 0, 1, 1),
 (2, 'PLANTA',                   N'PLANTA',                   0, 1, 2, 1),
 (3, 'OFICINAS ADMINISTRATIVAS', N'OFICINAS ADMINISTRATIVAS', 0, 1, 3, 1);
 
INSERT INTO dbo.Jornada (Id, Codigo, Nombre, DiasTrabajo, DiasDescanso, Orden, CreadoPorId) VALUES
 (1, 'TIPO_1',   N'Tipo 1 (22-8)',               22, 8, 1, 1),
 (2, 'TIPO_2',   N'Tipo 2 (11-4)',               11, 4, 2, 1),
 (3, 'TIPO_3',   N'Tipo 3 (5-2)',                 5, 2, 3, 1),
 (4, 'ESPECIAL', N'Especiales (3 días o menos)',  3, 0, 4, 1);
 
INSERT INTO dbo.RolAsignacion (Id, Codigo, Nombre, EsDescanso, Orden, CreadoPorId) VALUES
 (1, 'PRINCIPAL', N'Principal', 0, 1, 1),
 (2, 'BACK',      N'Back',      0, 2, 1),
 (3, 'DESCANSO',  N'Descanso',  1, 3, 1);
 
INSERT INTO dbo.TipoAplicacionNovedad (Id, Codigo, Nombre, Orden, CreadoPorId) VALUES
 (1, 'PERSONA',  N'Persona',  1, 1),
 (2, 'PROYECTO', N'Proyecto', 2, 1),
 (3, 'GENERAL',  N'General',  3, 1);
 
INSERT INTO dbo.OrigenNovedad (Id, Codigo, Nombre, EsEditable, Orden, CreadoPorId) VALUES
 (1, 'PROFESIOGRAMA',    N'PROFESIOGRAMA',         1, 1, 1),   -- "aplicacionProyectos"
 (2, 'PERMISOS_MEDICOS', N'App Permisos Médicos',  0, 2, 1);   -- "aplicacionPermisosMedicos"
 
INSERT INTO dbo.TipoNovedad
 (Id, Codigo, Nombre, Sigla, SiglaCronograma, ColorHex, CategoriaId, CategoriaDescripcion, EstadoReporte, ObservacionReporte,
  AplicaPersona, AplicaProyecto, AplicaGeneral, SeleccionableManual, Orden, CreadoPorId) VALUES
 (1, 'CALAMIDAD_DOMESTICA', N'CALAMIDAD DOMESTICA', 'CD',     'CD', '#8E44AD', 5, N'Asistencia Libre', 'LIBRE', N'CALAMIDAD DOMESTICA', 1, 0, 0, 1, 1, 1),
 (2, 'DESCANSO',            N'DESCANSO',            'D',      'D',  '#607D8B', 5, N'Asistencia Libre', 'LIBRE', N'DESCANSO',            1, 0, 0, 0, 2, 1),
 (3, 'FERIADO',             N'FERIADO',             'FER',    'F',  '#D13438', 5, N'Asistencia Libre', 'LIBRE', N'FERIADO',             0, 1, 1, 1, 3, 1),
 (4, 'PATERNIDAD',          N'PATERNIDAD',          'PTNDAD', 'PT', '#27AE60', 5, N'Asistencia Libre', 'LIBRE', N'PATERNIDAD',          1, 0, 0, 1, 4, 1),
 (5, 'PERMISO',             N'PERMISO',             'P',      'P',  '#FFB900', 5, N'Asistencia Libre', 'LIBRE', N'PERMISO',             1, 0, 0, 1, 5, 1),
 (6, 'VACACIONES',          N'VACACIONES',          'V',      'V',  '#0277BD', 5, N'Asistencia Libre', 'LIBRE', N'VACACIONES',          1, 0, 0, 1, 6, 1),
 (7, 'PERMISO_MEDICO',      N'PERMISO MEDICO',      'PM',     'PM', '#FFB900', 5, N'Asistencia Libre', 'LIBRE', N'PERMISO MEDICO',      1, 0, 0, 0, 7, 1);
 
INSERT INTO dbo.CargoInfor (Cargo, CodigoInfor, CreadoPorId) VALUES   -- valores fijos actuales; resto vía importación Excel
 (N'PARAMEDICO',                'P0021',  1),
 (N'DESARROLLADOR DE SOFTWARE', 'PPOOPP', 1),   -- [PENDIENTE] parece valor de prueba
 (N'SUPERVISOR SSA',            'P00030', 1);
 
INSERT INTO dbo.Departamento (Nombre, NombreCorto, Tipo, Orden, CreadoPorId) VALUES
 (N'DEPARTAMENTO DE INFRAESTRUCTURA',          N'Infraestructura Integral', 'DEPARTAMENTO', 1, 1),
 (N'DEPARTAMENTO SEDEMI TELECOM',              N'Telecomunicaciones',       'DEPARTAMENTO', 2, 1),
 (N'DEPARTAMENTO SEDEMI PETROLEO Y GAS',       N'Petróleo y Gas',           'DEPARTAMENTO', 3, 1),
 (N'DEPARTAMENTO SEDEMI ENERGIA',              N'Energía',                  'DEPARTAMENTO', 4, 1),
 (N'DEPARTAMENTO DE INFRAESTRUCTURA METALICA', N'Infraestructura Metálica', 'DEPARTAMENTO', 5, 1),
 (N'DEPARTAMENTO SEDEMI MINERIA',              N'Minería',                  'DEPARTAMENTO', 6, 1),
 (N'UNIDAD SISTEMA INTEGRADO DE GESTION',      N'SIG',                      'UNIDAD',       7, 1);
 
INSERT INTO dbo.Parametro (Clave, Valor, TipoDato, Descripcion, CreadoPorId) VALUES
 ('PROYECTO_MAX_PRINCIPALES',       N'20',        'INT',  N'Máximo de principales por proyecto', 1),
 ('PROYECTO_MAX_BACKS',             N'20',        'INT',  N'Máximo de backs por proyecto', 1),
 ('BACK_MAX_DIAS_DESCANSO',         N'20',        'INT',  N'Máximo de días de descanso posteriores de un back', 1),
 ('CRONOGRAMA_MAX_DIAS',            N'90',        'INT',  N'Rango máximo del cronograma en días', 1),
 ('PROYECTO_VISIBILIDAD',           N'CREADOR',   'TEXT', N'CREADOR = solo el propietario; DEPARTAMENTO = todo el departamento del proyecto', 1),
 ('ALMUERZO_SALIDA_OPCIONES',       N'11:00,12:00,13:00,14:00', 'LIST', N'Horas permitidas de salida a almuerzo', 1),
 ('ALMUERZO_REGRESO_OPCIONES',      N'12:00,13:00,14:00,15:00', 'LIST', N'Horas permitidas de regreso de almuerzo', 1),
 ('EMPLEADO_FAMILIAS_ASIGNABLES',   N'ADMINISTRATIVO',          'LIST', N'Familias de puesto que se pueden asignar a proyectos', 1),
 ('ZONA_HORARIA',                   N'SA Pacific Standard Time', 'TEXT', N'Zona horaria de negocio (Ecuador, UTC-5)', 1);
 
COMMIT TRANSACTION;
GO
 
/* ---------------------------------------------------------------------
   6. VISTAS DE LECTURA
   --------------------------------------------------------------------- */
 
CREATE OR ALTER VIEW dbo.vwProyectoResumen
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
    SELECT STRING_AGG(x.NombreCompleto, N' | ') AS Nombres
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
        CASE WHEN CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), '-05:00') AS DATE)
                  BETWEEN a.FechaInicio AND a.FechaFin THEN 0 ELSE 1 END,
        a.Version DESC
) act
WHERE p.Eliminado = 0;
GO
 
CREATE OR ALTER VIEW dbo.vwNovedadDiaVigente   -- base del cronograma y del reporte
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
JOIN dbo.OrigenNovedad o           ON o.Id   = n.OrigenNovedadId;
GO
 