using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace App.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "CargoInfor",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Cargo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CodigoInfor = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                        .Annotation("Relational:DefaultConstraintName", "DF_CargoInfor_Activo"),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_CargoInfor_FechaCreacion"),
                    ModificadoPorId = table.Column<int>(type: "int", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CargoInfor", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Compania",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    NombreComercial = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Ruc = table.Column<string>(type: "varchar(13)", unicode: false, maxLength: 13, nullable: true),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Compania_FechaCreacion"),
                    ModificadoPorId = table.Column<int>(type: "int", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Compania", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Departamento",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NombreCorto = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Tipo = table.Column<string>(type: "varchar(12)", unicode: false, maxLength: 12, nullable: false),
                    CodigoErp = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    Orden = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0)
                        .Annotation("Relational:DefaultConstraintName", "DF_Departamento_Orden"),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                        .Annotation("Relational:DefaultConstraintName", "DF_Departamento_Activo"),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Departamento_FechaCreacion"),
                    ModificadoPorId = table.Column<int>(type: "int", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departamento", x => x.Id);
                    table.CheckConstraint("CK_Departamento_Tipo", "[Tipo] IN ('DEPARTAMENTO','UNIDAD')");
                });

            migrationBuilder.CreateTable(
                name: "Empleado",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CodigoEkon = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Cedula = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    NombreCompleto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Apellidos = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Nombres = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CorreoEmpresa = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CodEmpresa = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    Empresa = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CodPuesto = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    Puesto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CodDepartamento = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    Departamento = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CodUnidad = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    Unidad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CodArea = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    Area = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CodSeccion = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: true),
                    Seccion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FamiliaPuesto = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EstadoErp = table.Column<string>(type: "char(1)", unicode: false, fixedLength: true, maxLength: 1, nullable: true),
                    EsOrigenLegado = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                        .Annotation("Relational:DefaultConstraintName", "DF_Empleado_EsOrigenLegado"),
                    FechaSincronizacion = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Empleado_FechaCreacion"),
                    ModificadoPorId = table.Column<int>(type: "int", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Empleado", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuario",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntraObjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NombreMostrar = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmpleadoId = table.Column<int>(type: "int", nullable: true),
                    EsSistema = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                        .Annotation("Relational:DefaultConstraintName", "DF_Usuario_EsSistema"),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                        .Annotation("Relational:DefaultConstraintName", "DF_Usuario_Activo"),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Usuario_FechaCreacion"),
                    ModificadoPorId = table.Column<int>(type: "int", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Usuario_CreadoPor",
                        column: x => x.CreadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Usuario_Empleado",
                        column: x => x.EmpleadoId,
                        principalSchema: "dbo",
                        principalTable: "Empleado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Usuario_ModificadoPor",
                        column: x => x.ModificadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EstadoProyecto",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    EsVigente = table.Column<bool>(type: "bit", nullable: false),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_EstadoProyecto_FechaCreacion"),
                    ModificadoPorId = table.Column<int>(type: "int", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    Codigo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                        .Annotation("Relational:DefaultConstraintName", "DF_EstadoProyecto_Activo")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EstadoProyecto", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EstadoProyecto_CreadoPor",
                        column: x => x.CreadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EstadoProyecto_ModificadoPor",
                        column: x => x.ModificadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GrupoProyecto",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    RequiereProyectoErp = table.Column<bool>(type: "bit", nullable: false),
                    RequiereDimension = table.Column<bool>(type: "bit", nullable: false),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_GrupoProyecto_FechaCreacion"),
                    ModificadoPorId = table.Column<int>(type: "int", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    Codigo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                        .Annotation("Relational:DefaultConstraintName", "DF_GrupoProyecto_Activo")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrupoProyecto", x => x.Id);
                    table.CheckConstraint("CK_GrupoProyecto_Requisito", "[RequiereProyectoErp] = 1 OR [RequiereDimension] = 1");
                    table.ForeignKey(
                        name: "FK_GrupoProyecto_CreadoPor",
                        column: x => x.CreadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GrupoProyecto_ModificadoPor",
                        column: x => x.ModificadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Jornada",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    DiasTrabajo = table.Column<byte>(type: "tinyint", nullable: false),
                    DiasDescanso = table.Column<byte>(type: "tinyint", nullable: false),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Jornada_FechaCreacion"),
                    ModificadoPorId = table.Column<int>(type: "int", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    Codigo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                        .Annotation("Relational:DefaultConstraintName", "DF_Jornada_Activo")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jornada", x => x.Id);
                    table.CheckConstraint("CK_Jornada_Dias", "[DiasTrabajo] > 0 AND [DiasDescanso] >= 0");
                    table.ForeignKey(
                        name: "FK_Jornada_CreadoPor",
                        column: x => x.CreadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Jornada_ModificadoPor",
                        column: x => x.ModificadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrigenNovedad",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    EsEditable = table.Column<bool>(type: "bit", nullable: false),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_OrigenNovedad_FechaCreacion"),
                    ModificadoPorId = table.Column<int>(type: "int", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    Codigo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                        .Annotation("Relational:DefaultConstraintName", "DF_OrigenNovedad_Activo")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrigenNovedad", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrigenNovedad_CreadoPor",
                        column: x => x.CreadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrigenNovedad_ModificadoPor",
                        column: x => x.ModificadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Parametro",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Clave = table.Column<string>(type: "varchar(60)", unicode: false, maxLength: 60, nullable: false),
                    Valor = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    TipoDato = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Parametro_FechaCreacion"),
                    ModificadoPorId = table.Column<int>(type: "int", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parametro", x => x.Id);
                    table.CheckConstraint("CK_Parametro_TipoDato", "[TipoDato] IN ('INT','BOOL','TEXT','LIST')");
                    table.ForeignKey(
                        name: "FK_Parametro_CreadoPor",
                        column: x => x.CreadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Parametro_ModificadoPor",
                        column: x => x.ModificadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolAsignacion",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    EsDescanso = table.Column<bool>(type: "bit", nullable: false),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_RolAsignacion_FechaCreacion"),
                    ModificadoPorId = table.Column<int>(type: "int", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    Codigo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                        .Annotation("Relational:DefaultConstraintName", "DF_RolAsignacion_Activo")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolAsignacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RolAsignacion_CreadoPor",
                        column: x => x.CreadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolAsignacion_ModificadoPor",
                        column: x => x.ModificadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TipoAplicacionNovedad",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_TipoAplicacionNovedad_FechaCreacion"),
                    ModificadoPorId = table.Column<int>(type: "int", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    Codigo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                        .Annotation("Relational:DefaultConstraintName", "DF_TipoAplicacionNovedad_Activo")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipoAplicacionNovedad", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TipoAplicacionNovedad_CreadoPor",
                        column: x => x.CreadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TipoAplicacionNovedad_ModificadoPor",
                        column: x => x.ModificadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TipoMovimiento",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_TipoMovimiento_FechaCreacion"),
                    ModificadoPorId = table.Column<int>(type: "int", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    Codigo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                        .Annotation("Relational:DefaultConstraintName", "DF_TipoMovimiento_Activo")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipoMovimiento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TipoMovimiento_CreadoPor",
                        column: x => x.CreadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TipoMovimiento_ModificadoPor",
                        column: x => x.ModificadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TipoNovedad",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    Sigla = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false),
                    SiglaCronograma = table.Column<string>(type: "varchar(4)", unicode: false, maxLength: 4, nullable: false),
                    ColorHex = table.Column<string>(type: "char(7)", unicode: false, fixedLength: true, maxLength: 7, nullable: false),
                    CategoriaId = table.Column<int>(type: "int", nullable: false),
                    CategoriaDescripcion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EstadoReporte = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    ObservacionReporte = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    AplicaPersona = table.Column<bool>(type: "bit", nullable: false),
                    AplicaProyecto = table.Column<bool>(type: "bit", nullable: false),
                    AplicaGeneral = table.Column<bool>(type: "bit", nullable: false),
                    SeleccionableManual = table.Column<bool>(type: "bit", nullable: false),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_TipoNovedad_FechaCreacion"),
                    ModificadoPorId = table.Column<int>(type: "int", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    Codigo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Orden = table.Column<byte>(type: "tinyint", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                        .Annotation("Relational:DefaultConstraintName", "DF_TipoNovedad_Activo")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TipoNovedad", x => x.Id);
                    table.CheckConstraint("CK_TipoNovedad_ColorHex", "[ColorHex] LIKE '#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]'");
                    table.ForeignKey(
                        name: "FK_TipoNovedad_CreadoPor",
                        column: x => x.CreadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TipoNovedad_ModificadoPor",
                        column: x => x.ModificadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UsuarioDepartamento",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    DepartamentoId = table.Column<int>(type: "int", nullable: false),
                    Notificado = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                        .Annotation("Relational:DefaultConstraintName", "DF_UsuarioDepartamento_Notificado"),
                    FechaNotificacion = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                        .Annotation("Relational:DefaultConstraintName", "DF_UsuarioDepartamento_Activo"),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_UsuarioDepartamento_FechaCreacion"),
                    ModificadoPorId = table.Column<int>(type: "int", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioDepartamento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsuarioDepartamento_CreadoPor",
                        column: x => x.CreadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UsuarioDepartamento_Departamento",
                        column: x => x.DepartamentoId,
                        principalSchema: "dbo",
                        principalTable: "Departamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UsuarioDepartamento_ModificadoPor",
                        column: x => x.ModificadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UsuarioDepartamento_Usuario",
                        column: x => x.UsuarioId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Proyecto",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Uid = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Proyecto_Uid"),
                    Codigo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    NombreVisual = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    GrupoProyectoId = table.Column<byte>(type: "tinyint", nullable: false),
                    CompaniaId = table.Column<int>(type: "int", nullable: false),
                    ProyectoErpId = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    ProyectoErpNombre = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ProyectoErpEstado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DimensionUegpId = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    DimensionDescripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFin = table.Column<DateOnly>(type: "date", nullable: false),
                    EstadoProyectoId = table.Column<byte>(type: "tinyint", nullable: false),
                    HorarioCodigo = table.Column<int>(type: "int", nullable: true),
                    HorarioDescripcion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    HoraEntrada = table.Column<TimeOnly>(type: "time(0)", nullable: true),
                    HoraSalida = table.Column<TimeOnly>(type: "time(0)", nullable: true),
                    HorasJornadaMin = table.Column<short>(type: "smallint", nullable: true),
                    HorasTrabajadasMin = table.Column<short>(type: "smallint", nullable: true),
                    TipoHorario = table.Column<string>(type: "varchar(5)", unicode: false, maxLength: 5, nullable: true),
                    SalidaAlmuerzo = table.Column<TimeOnly>(type: "time(0)", nullable: true),
                    RegresoAlmuerzo = table.Column<TimeOnly>(type: "time(0)", nullable: true),
                    DepartamentoId = table.Column<int>(type: "int", nullable: true),
                    PropietarioUsuarioId = table.Column<int>(type: "int", nullable: false),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                        .Annotation("Relational:DefaultConstraintName", "DF_Proyecto_Eliminado"),
                    LegacyId = table.Column<int>(type: "int", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Proyecto_FechaCreacion"),
                    ModificadoPorId = table.Column<int>(type: "int", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proyecto", x => x.Id);
                    table.CheckConstraint("CK_Proyecto_Almuerzo", "[SalidaAlmuerzo] IS NULL OR [RegresoAlmuerzo] IS NULL OR [RegresoAlmuerzo] > [SalidaAlmuerzo]");
                    table.CheckConstraint("CK_Proyecto_Fechas", "[FechaFin] >= [FechaInicio]");
                    table.CheckConstraint("CK_Proyecto_Origen", "[ProyectoErpId] IS NOT NULL OR [DimensionUegpId] IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_Proyecto_Compania",
                        column: x => x.CompaniaId,
                        principalSchema: "dbo",
                        principalTable: "Compania",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Proyecto_CreadoPor",
                        column: x => x.CreadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Proyecto_Departamento",
                        column: x => x.DepartamentoId,
                        principalSchema: "dbo",
                        principalTable: "Departamento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Proyecto_EstadoProyecto",
                        column: x => x.EstadoProyectoId,
                        principalSchema: "dbo",
                        principalTable: "EstadoProyecto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Proyecto_GrupoProyecto",
                        column: x => x.GrupoProyectoId,
                        principalSchema: "dbo",
                        principalTable: "GrupoProyecto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Proyecto_ModificadoPor",
                        column: x => x.ModificadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Proyecto_Propietario",
                        column: x => x.PropietarioUsuarioId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Novedad",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Uid = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Novedad_Uid"),
                    Codigo = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    TipoAplicacionNovedadId = table.Column<byte>(type: "tinyint", nullable: false),
                    TipoNovedadId = table.Column<byte>(type: "tinyint", nullable: false),
                    OrigenNovedadId = table.Column<byte>(type: "tinyint", nullable: false),
                    EmpleadoId = table.Column<int>(type: "int", nullable: true),
                    ProyectoId = table.Column<int>(type: "int", nullable: true),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFin = table.Column<DateOnly>(type: "date", nullable: false),
                    Observacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OrigenArea = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RegistradoPorNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RegistradoPorCorreo = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DetalleOrigenJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Anulada = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                        .Annotation("Relational:DefaultConstraintName", "DF_Novedad_Anulada"),
                    FechaAnulacion = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    AnuladaPorId = table.Column<int>(type: "int", nullable: true),
                    MotivoAnulacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LegacyId = table.Column<int>(type: "int", nullable: true),
                    RowVer = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_Novedad_FechaCreacion"),
                    ModificadoPorId = table.Column<int>(type: "int", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Novedad", x => x.Id);
                    table.CheckConstraint("CK_Novedad_Anulacion", "[Anulada] = 0 OR ([FechaAnulacion] IS NOT NULL AND [AnuladaPorId] IS NOT NULL)");
                    table.CheckConstraint("CK_Novedad_Aplicacion", "([TipoAplicacionNovedadId] = 1 AND [EmpleadoId] IS NOT NULL AND [ProyectoId] IS NULL) OR ([TipoAplicacionNovedadId] = 2 AND [ProyectoId] IS NOT NULL AND [EmpleadoId] IS NULL) OR ([TipoAplicacionNovedadId] = 3 AND [ProyectoId] IS NULL AND [EmpleadoId] IS NULL)");
                    table.CheckConstraint("CK_Novedad_DetalleJson", "[DetalleOrigenJson] IS NULL OR ISJSON([DetalleOrigenJson]) = 1");
                    table.CheckConstraint("CK_Novedad_Fechas", "[FechaFin] >= [FechaInicio]");
                    table.ForeignKey(
                        name: "FK_Novedad_AnuladaPor",
                        column: x => x.AnuladaPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Novedad_CreadoPor",
                        column: x => x.CreadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Novedad_Empleado",
                        column: x => x.EmpleadoId,
                        principalSchema: "dbo",
                        principalTable: "Empleado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Novedad_ModificadoPor",
                        column: x => x.ModificadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Novedad_Origen",
                        column: x => x.OrigenNovedadId,
                        principalSchema: "dbo",
                        principalTable: "OrigenNovedad",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Novedad_Proyecto",
                        column: x => x.ProyectoId,
                        principalSchema: "dbo",
                        principalTable: "Proyecto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Novedad_TipoAplicacion",
                        column: x => x.TipoAplicacionNovedadId,
                        principalSchema: "dbo",
                        principalTable: "TipoAplicacionNovedad",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Novedad_TipoNovedad",
                        column: x => x.TipoNovedadId,
                        principalSchema: "dbo",
                        principalTable: "TipoNovedad",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProyectoActividad",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProyectoId = table.Column<int>(type: "int", nullable: false),
                    MovimientoUid = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ProyectoActividad_Uid"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    TipoMovimientoId = table.Column<byte>(type: "tinyint", nullable: false),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFin = table.Column<DateOnly>(type: "date", nullable: false),
                    ActividadCodigo = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    ActividadDescripcion = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ActividadTipo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LegacyId = table.Column<int>(type: "int", nullable: true),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ProyectoActividad_FechaCreacion"),
                    ModificadoPorId = table.Column<int>(type: "int", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProyectoActividad", x => x.Id);
                    table.CheckConstraint("CK_ProyectoActividad_Fechas", "[FechaFin] >= [FechaInicio]");
                    table.ForeignKey(
                        name: "FK_ProyectoActividad_CreadoPor",
                        column: x => x.CreadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProyectoActividad_ModificadoPor",
                        column: x => x.ModificadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProyectoActividad_Proyecto",
                        column: x => x.ProyectoId,
                        principalSchema: "dbo",
                        principalTable: "Proyecto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProyectoActividad_TipoMovimiento",
                        column: x => x.TipoMovimientoId,
                        principalSchema: "dbo",
                        principalTable: "TipoMovimiento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProyectoEtapa",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProyectoId = table.Column<int>(type: "int", nullable: false),
                    EtapaUid = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ProyectoEtapa_EtapaUid"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    TipoMovimientoId = table.Column<byte>(type: "tinyint", nullable: false),
                    EstadoProyectoId = table.Column<byte>(type: "tinyint", nullable: false),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFin = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaCorte = table.Column<DateOnly>(type: "date", nullable: true),
                    ActividadCodigo = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    SnapshotPersonal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LegacyId = table.Column<int>(type: "int", nullable: true),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ProyectoEtapa_FechaCreacion"),
                    ModificadoPorId = table.Column<int>(type: "int", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProyectoEtapa", x => x.Id);
                    table.CheckConstraint("CK_ProyectoEtapa_Fechas", "[FechaFin] >= [FechaInicio]");
                    table.CheckConstraint("CK_ProyectoEtapa_Snapshot", "[SnapshotPersonal] IS NULL OR ISJSON([SnapshotPersonal]) = 1");
                    table.ForeignKey(
                        name: "FK_ProyectoEtapa_CreadoPor",
                        column: x => x.CreadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProyectoEtapa_Estado",
                        column: x => x.EstadoProyectoId,
                        principalSchema: "dbo",
                        principalTable: "EstadoProyecto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProyectoEtapa_ModificadoPor",
                        column: x => x.ModificadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProyectoEtapa_Proyecto",
                        column: x => x.ProyectoId,
                        principalSchema: "dbo",
                        principalTable: "Proyecto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProyectoEtapa_TipoMovimiento",
                        column: x => x.TipoMovimientoId,
                        principalSchema: "dbo",
                        principalTable: "TipoMovimiento",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProyectoPersonal",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProyectoId = table.Column<int>(type: "int", nullable: false),
                    RolAsignacionId = table.Column<byte>(type: "tinyint", nullable: false),
                    Numero = table.Column<short>(type: "smallint", nullable: false),
                    EmpleadoId = table.Column<int>(type: "int", nullable: false),
                    CargoAsignado = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFin = table.Column<DateOnly>(type: "date", nullable: false),
                    JornadaId = table.Column<byte>(type: "tinyint", nullable: true),
                    DiasTrabajo = table.Column<byte>(type: "tinyint", nullable: true),
                    DiasDescanso = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)0)
                        .Annotation("Relational:DefaultConstraintName", "DF_ProyectoPersonal_DiasDescanso"),
                    EsPrincipalInicial = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                        .Annotation("Relational:DefaultConstraintName", "DF_ProyectoPersonal_EsPrincipalInicial"),
                    TipoRegistro = table.Column<string>(type: "varchar(10)", unicode: false, maxLength: 10, nullable: false, defaultValue: "JORNADA")
                        .Annotation("Relational:DefaultConstraintName", "DF_ProyectoPersonal_TipoRegistro"),
                    PrincipalRelacionadoId = table.Column<int>(type: "int", nullable: true),
                    Observacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LegacyId = table.Column<int>(type: "int", nullable: true),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ProyectoPersonal_FechaCreacion"),
                    ModificadoPorId = table.Column<int>(type: "int", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProyectoPersonal", x => x.Id);
                    table.UniqueConstraint("UQ_ProyectoPersonal_Clave", x => new { x.Id, x.ProyectoId, x.EmpleadoId });
                    table.CheckConstraint("CK_ProyectoPersonal_Fechas", "[FechaFin] >= [FechaInicio]");
                    table.CheckConstraint("CK_ProyectoPersonal_Inicial", "[EsPrincipalInicial] = 0 OR [RolAsignacionId] = 1");
                    table.CheckConstraint("CK_ProyectoPersonal_Jornada", "[RolAsignacionId] <> 1 OR ([JornadaId] IS NOT NULL AND [DiasTrabajo] > 0)");
                    table.CheckConstraint("CK_ProyectoPersonal_Rol", "[RolAsignacionId] IN (1, 2)");
                    table.CheckConstraint("CK_ProyectoPersonal_TipoRegistro", "[TipoRegistro] IN ('JORNADA','DESCANSO')");
                    table.ForeignKey(
                        name: "FK_ProyectoPersonal_CreadoPor",
                        column: x => x.CreadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProyectoPersonal_Empleado",
                        column: x => x.EmpleadoId,
                        principalSchema: "dbo",
                        principalTable: "Empleado",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProyectoPersonal_Jornada",
                        column: x => x.JornadaId,
                        principalSchema: "dbo",
                        principalTable: "Jornada",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProyectoPersonal_ModificadoPor",
                        column: x => x.ModificadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProyectoPersonal_Principal",
                        column: x => x.PrincipalRelacionadoId,
                        principalSchema: "dbo",
                        principalTable: "ProyectoPersonal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProyectoPersonal_Proyecto",
                        column: x => x.ProyectoId,
                        principalSchema: "dbo",
                        principalTable: "Proyecto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProyectoPersonal_Rol",
                        column: x => x.RolAsignacionId,
                        principalSchema: "dbo",
                        principalTable: "RolAsignacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NovedadDia",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NovedadId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_NovedadDia_FechaCreacion"),
                    ModificadoPorId = table.Column<int>(type: "int", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NovedadDia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NovedadDia_CreadoPor",
                        column: x => x.CreadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NovedadDia_ModificadoPor",
                        column: x => x.ModificadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NovedadDia_Novedad",
                        column: x => x.NovedadId,
                        principalSchema: "dbo",
                        principalTable: "Novedad",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProyectoAsignacionDia",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProyectoId = table.Column<int>(type: "int", nullable: false),
                    ProyectoPersonalId = table.Column<int>(type: "int", nullable: false),
                    EmpleadoId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    RolAsignacionId = table.Column<byte>(type: "tinyint", nullable: false),
                    TipoAsignacion = table.Column<string>(type: "varchar(6)", unicode: false, maxLength: 6, nullable: false),
                    Bloque = table.Column<short>(type: "smallint", nullable: false),
                    LegacyId = table.Column<int>(type: "int", nullable: true),
                    CreadoPorId = table.Column<int>(type: "int", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "datetime2(0)", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                        .Annotation("Relational:DefaultConstraintName", "DF_ProyectoAsignacionDia_FechaCreacion"),
                    ModificadoPorId = table.Column<int>(type: "int", nullable: true),
                    FechaModificacion = table.Column<DateTime>(type: "datetime2(0)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProyectoAsignacionDia", x => x.Id);
                    table.CheckConstraint("CK_ProyectoAsignacionDia_Tipo", "[TipoAsignacion] IN ('AUTO','MANUAL')");
                    table.ForeignKey(
                        name: "FK_ProyectoAsignacionDia_CreadoPor",
                        column: x => x.CreadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProyectoAsignacionDia_ModificadoPor",
                        column: x => x.ModificadoPorId,
                        principalSchema: "dbo",
                        principalTable: "Usuario",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProyectoAsignacionDia_Personal",
                        columns: x => new { x.ProyectoPersonalId, x.ProyectoId, x.EmpleadoId },
                        principalSchema: "dbo",
                        principalTable: "ProyectoPersonal",
                        principalColumns: new[] { "Id", "ProyectoId", "EmpleadoId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProyectoAsignacionDia_Proyecto",
                        column: x => x.ProyectoId,
                        principalSchema: "dbo",
                        principalTable: "Proyecto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProyectoAsignacionDia_Rol",
                        column: x => x.RolAsignacionId,
                        principalSchema: "dbo",
                        principalTable: "RolAsignacion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "Usuario",
                columns: new[] { "Id", "Activo", "CreadoPorId", "Email", "EmpleadoId", "EntraObjectId", "EsSistema", "FechaCreacion", "FechaModificacion", "ModificadoPorId", "NombreMostrar" },
                values: new object[] { 1, true, 1, "sistema@profesiograma.local", null, null, true, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "SISTEMA" });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "CargoInfor",
                columns: new[] { "Id", "Activo", "Cargo", "CodigoInfor", "CreadoPorId", "FechaCreacion", "FechaModificacion", "ModificadoPorId" },
                values: new object[,]
                {
                    { 1, true, "PARAMEDICO", "P0021", 1, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null },
                    { 2, true, "DESARROLLADOR DE SOFTWARE", "PPOOPP", 1, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null },
                    { 3, true, "SUPERVISOR SSA", "P00030", 1, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null }
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "Departamento",
                columns: new[] { "Id", "Activo", "CodigoErp", "CreadoPorId", "FechaCreacion", "FechaModificacion", "ModificadoPorId", "Nombre", "NombreCorto", "Orden", "Tipo" },
                values: new object[,]
                {
                    { 1, true, null, 1, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "DEPARTAMENTO DE INFRAESTRUCTURA", "Infraestructura Integral", (short)1, "DEPARTAMENTO" },
                    { 2, true, null, 1, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "DEPARTAMENTO SEDEMI TELECOM", "Telecomunicaciones", (short)2, "DEPARTAMENTO" },
                    { 3, true, null, 1, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "DEPARTAMENTO SEDEMI PETROLEO Y GAS", "Petróleo y Gas", (short)3, "DEPARTAMENTO" },
                    { 4, true, null, 1, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "DEPARTAMENTO SEDEMI ENERGIA", "Energía", (short)4, "DEPARTAMENTO" },
                    { 5, true, null, 1, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "DEPARTAMENTO DE INFRAESTRUCTURA METALICA", "Infraestructura Metálica", (short)5, "DEPARTAMENTO" },
                    { 6, true, null, 1, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "DEPARTAMENTO SEDEMI MINERIA", "Minería", (short)6, "DEPARTAMENTO" },
                    { 7, true, null, 1, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "UNIDAD SISTEMA INTEGRADO DE GESTION", "SIG", (short)7, "UNIDAD" }
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "EstadoProyecto",
                columns: new[] { "Id", "Activo", "Codigo", "CreadoPorId", "EsVigente", "FechaCreacion", "FechaModificacion", "ModificadoPorId", "Nombre", "Orden" },
                values: new object[,]
                {
                    { (byte)1, true, "ACTIVO", 1, true, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Activo", (byte)1 },
                    { (byte)2, true, "SUSPENDIDO", 1, true, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Suspendido", (byte)2 },
                    { (byte)3, true, "INACTIVO", 1, false, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Inactivo", (byte)3 },
                    { (byte)4, true, "TERMINADO", 1, false, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Terminado", (byte)4 }
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "GrupoProyecto",
                columns: new[] { "Id", "Activo", "Codigo", "CreadoPorId", "FechaCreacion", "FechaModificacion", "ModificadoPorId", "Nombre", "Orden", "RequiereDimension", "RequiereProyectoErp" },
                values: new object[,]
                {
                    { (byte)1, true, "CAMPO", 1, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "CAMPO", (byte)1, false, true },
                    { (byte)2, true, "PLANTA", 1, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "PLANTA", (byte)2, true, false },
                    { (byte)3, true, "OFICINAS ADMINISTRATIVAS", 1, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "OFICINAS ADMINISTRATIVAS", (byte)3, true, false }
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "Jornada",
                columns: new[] { "Id", "Activo", "Codigo", "CreadoPorId", "DiasDescanso", "DiasTrabajo", "FechaCreacion", "FechaModificacion", "ModificadoPorId", "Nombre", "Orden" },
                values: new object[,]
                {
                    { (byte)1, true, "TIPO_1", 1, (byte)8, (byte)22, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Tipo 1 (22-8)", (byte)1 },
                    { (byte)2, true, "TIPO_2", 1, (byte)4, (byte)11, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Tipo 2 (11-4)", (byte)2 },
                    { (byte)3, true, "TIPO_3", 1, (byte)2, (byte)5, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Tipo 3 (5-2)", (byte)3 },
                    { (byte)4, true, "ESPECIAL", 1, (byte)0, (byte)3, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Especiales (3 días o menos)", (byte)4 }
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "OrigenNovedad",
                columns: new[] { "Id", "Activo", "Codigo", "CreadoPorId", "EsEditable", "FechaCreacion", "FechaModificacion", "ModificadoPorId", "Nombre", "Orden" },
                values: new object[,]
                {
                    { (byte)1, true, "PROFESIOGRAMA", 1, true, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "PROFESIOGRAMA", (byte)1 },
                    { (byte)2, true, "PERMISOS_MEDICOS", 1, false, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "App Permisos Médicos", (byte)2 }
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "Parametro",
                columns: new[] { "Id", "Clave", "CreadoPorId", "Descripcion", "FechaCreacion", "FechaModificacion", "ModificadoPorId", "TipoDato", "Valor" },
                values: new object[,]
                {
                    { 1, "PROYECTO_MAX_PRINCIPALES", 1, "Máximo de principales por proyecto", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "INT", "20" },
                    { 2, "PROYECTO_MAX_BACKS", 1, "Máximo de backs por proyecto", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "INT", "20" },
                    { 3, "BACK_MAX_DIAS_DESCANSO", 1, "Máximo de días de descanso posteriores de un back", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "INT", "20" },
                    { 4, "CRONOGRAMA_MAX_DIAS", 1, "Rango máximo del cronograma en días", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "INT", "90" },
                    { 5, "PROYECTO_VISIBILIDAD", 1, "CREADOR = solo el propietario; DEPARTAMENTO = todo el departamento del proyecto", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "TEXT", "CREADOR" },
                    { 6, "ALMUERZO_SALIDA_OPCIONES", 1, "Horas permitidas de salida a almuerzo", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "LIST", "11:00,12:00,13:00,14:00" },
                    { 7, "ALMUERZO_REGRESO_OPCIONES", 1, "Horas permitidas de regreso de almuerzo", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "LIST", "12:00,13:00,14:00,15:00" },
                    { 8, "EMPLEADO_FAMILIAS_ASIGNABLES", 1, "Familias de puesto que se pueden asignar a proyectos", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "LIST", "ADMINISTRATIVO" },
                    { 9, "ZONA_HORARIA", 1, "Zona horaria de negocio (Ecuador, UTC-5)", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "TEXT", "SA Pacific Standard Time" }
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "RolAsignacion",
                columns: new[] { "Id", "Activo", "Codigo", "CreadoPorId", "EsDescanso", "FechaCreacion", "FechaModificacion", "ModificadoPorId", "Nombre", "Orden" },
                values: new object[,]
                {
                    { (byte)1, true, "PRINCIPAL", 1, false, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Principal", (byte)1 },
                    { (byte)2, true, "BACK", 1, false, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Back", (byte)2 },
                    { (byte)3, true, "DESCANSO", 1, true, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Descanso", (byte)3 }
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "TipoAplicacionNovedad",
                columns: new[] { "Id", "Activo", "Codigo", "CreadoPorId", "FechaCreacion", "FechaModificacion", "ModificadoPorId", "Nombre", "Orden" },
                values: new object[,]
                {
                    { (byte)1, true, "PERSONA", 1, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Persona", (byte)1 },
                    { (byte)2, true, "PROYECTO", 1, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Proyecto", (byte)2 },
                    { (byte)3, true, "GENERAL", 1, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "General", (byte)3 }
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "TipoMovimiento",
                columns: new[] { "Id", "Activo", "Codigo", "CreadoPorId", "FechaCreacion", "FechaModificacion", "ModificadoPorId", "Nombre", "Orden" },
                values: new object[,]
                {
                    { (byte)1, true, "CREACION", 1, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Creación", (byte)1 },
                    { (byte)2, true, "ACTUALIZACION_PERSONAL", 1, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Actualización de personal", (byte)2 },
                    { (byte)3, true, "SUSPENSION", 1, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Suspensión", (byte)3 },
                    { (byte)4, true, "CIERRE", 1, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Cierre", (byte)4 },
                    { (byte)5, true, "REACTIVACION", 1, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Reactivación", (byte)5 },
                    { (byte)6, true, "CAMBIO_ACTIVIDAD", 1, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Cambio de actividad", (byte)6 },
                    { (byte)7, true, "EDICION_CABECERA", 1, new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "Edición de datos generales", (byte)7 }
                });

            migrationBuilder.InsertData(
                schema: "dbo",
                table: "TipoNovedad",
                columns: new[] { "Id", "Activo", "AplicaGeneral", "AplicaPersona", "AplicaProyecto", "CategoriaDescripcion", "CategoriaId", "Codigo", "ColorHex", "CreadoPorId", "EstadoReporte", "FechaCreacion", "FechaModificacion", "ModificadoPorId", "Nombre", "ObservacionReporte", "Orden", "SeleccionableManual", "Sigla", "SiglaCronograma" },
                values: new object[,]
                {
                    { (byte)1, true, false, true, false, "Asistencia Libre", 5, "CALAMIDAD_DOMESTICA", "#8E44AD", 1, "LIBRE", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "CALAMIDAD DOMESTICA", "CALAMIDAD DOMESTICA", (byte)1, true, "CD", "CD" },
                    { (byte)2, true, false, true, false, "Asistencia Libre", 5, "DESCANSO", "#607D8B", 1, "LIBRE", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "DESCANSO", "DESCANSO", (byte)2, false, "D", "D" },
                    { (byte)3, true, true, false, true, "Asistencia Libre", 5, "FERIADO", "#D13438", 1, "LIBRE", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "FERIADO", "FERIADO", (byte)3, true, "FER", "F" },
                    { (byte)4, true, false, true, false, "Asistencia Libre", 5, "PATERNIDAD", "#27AE60", 1, "LIBRE", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "PATERNIDAD", "PATERNIDAD", (byte)4, true, "PTNDAD", "PT" },
                    { (byte)5, true, false, true, false, "Asistencia Libre", 5, "PERMISO", "#FFB900", 1, "LIBRE", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "PERMISO", "PERMISO", (byte)5, true, "P", "P" },
                    { (byte)6, true, false, true, false, "Asistencia Libre", 5, "VACACIONES", "#0277BD", 1, "LIBRE", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "VACACIONES", "VACACIONES", (byte)6, true, "V", "V" },
                    { (byte)7, true, false, true, false, "Asistencia Libre", 5, "PERMISO_MEDICO", "#FFB900", 1, "LIBRE", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "PERMISO MEDICO", "PERMISO MEDICO", (byte)7, false, "PM", "PM" }
                });

            migrationBuilder.CreateIndex(
                name: "UQ_CargoInfor_Cargo",
                schema: "dbo",
                table: "CargoInfor",
                column: "Cargo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Departamento_Nombre",
                schema: "dbo",
                table: "Departamento",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Empleado_Busqueda",
                schema: "dbo",
                table: "Empleado",
                columns: new[] { "EstadoErp", "FamiliaPuesto" })
                .Annotation("SqlServer:Include", new[] { "NombreCompleto", "CodigoEkon", "Departamento", "Unidad", "Puesto" });

            migrationBuilder.CreateIndex(
                name: "IX_Empleado_Cedula",
                schema: "dbo",
                table: "Empleado",
                column: "Cedula");

            migrationBuilder.CreateIndex(
                name: "IX_Empleado_CorreoEmpresa",
                schema: "dbo",
                table: "Empleado",
                column: "CorreoEmpresa");

            migrationBuilder.CreateIndex(
                name: "UQ_Empleado_CodigoEkon",
                schema: "dbo",
                table: "Empleado",
                column: "CodigoEkon",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_EstadoProyecto_Codigo",
                schema: "dbo",
                table: "EstadoProyecto",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_GrupoProyecto_Codigo",
                schema: "dbo",
                table: "GrupoProyecto",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Jornada_Codigo",
                schema: "dbo",
                table: "Jornada",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Novedad_Empleado",
                schema: "dbo",
                table: "Novedad",
                column: "EmpleadoId",
                filter: "[EmpleadoId] IS NOT NULL AND [Anulada] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Novedad_Proyecto",
                schema: "dbo",
                table: "Novedad",
                column: "ProyectoId",
                filter: "[ProyectoId] IS NOT NULL AND [Anulada] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Novedad_Rango",
                schema: "dbo",
                table: "Novedad",
                columns: new[] { "FechaInicio", "FechaFin" },
                filter: "[Anulada] = 0")
                .Annotation("SqlServer:Include", new[] { "TipoAplicacionNovedadId", "TipoNovedadId", "EmpleadoId", "ProyectoId" });

            migrationBuilder.CreateIndex(
                name: "UQ_Novedad_Codigo",
                schema: "dbo",
                table: "Novedad",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Novedad_Uid",
                schema: "dbo",
                table: "Novedad",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Novedad_LegacyId",
                schema: "dbo",
                table: "Novedad",
                columns: new[] { "LegacyId", "OrigenNovedadId" },
                unique: true,
                filter: "[LegacyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NovedadDia_Fecha",
                schema: "dbo",
                table: "NovedadDia",
                column: "Fecha")
                .Annotation("SqlServer:Include", new[] { "NovedadId" });

            migrationBuilder.CreateIndex(
                name: "UQ_NovedadDia",
                schema: "dbo",
                table: "NovedadDia",
                columns: new[] { "NovedadId", "Fecha" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_OrigenNovedad_Codigo",
                schema: "dbo",
                table: "OrigenNovedad",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Parametro_Clave",
                schema: "dbo",
                table: "Parametro",
                column: "Clave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Proyecto_Departamento",
                schema: "dbo",
                table: "Proyecto",
                column: "DepartamentoId",
                filter: "[Eliminado] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Proyecto_Estado",
                schema: "dbo",
                table: "Proyecto",
                column: "EstadoProyectoId",
                filter: "[Eliminado] = 0")
                .Annotation("SqlServer:Include", new[] { "Codigo", "NombreVisual", "FechaInicio", "FechaFin" });

            migrationBuilder.CreateIndex(
                name: "IX_Proyecto_Nombre",
                schema: "dbo",
                table: "Proyecto",
                column: "NombreVisual");

            migrationBuilder.CreateIndex(
                name: "IX_Proyecto_Propietario",
                schema: "dbo",
                table: "Proyecto",
                column: "PropietarioUsuarioId",
                filter: "[Eliminado] = 0");

            migrationBuilder.CreateIndex(
                name: "UQ_Proyecto_Codigo",
                schema: "dbo",
                table: "Proyecto",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Proyecto_Uid",
                schema: "dbo",
                table: "Proyecto",
                column: "Uid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Proyecto_LegacyId",
                schema: "dbo",
                table: "Proyecto",
                column: "LegacyId",
                unique: true,
                filter: "[LegacyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProyectoActividad_Vigencia",
                schema: "dbo",
                table: "ProyectoActividad",
                columns: new[] { "ProyectoId", "FechaInicio", "FechaFin" })
                .Annotation("SqlServer:Include", new[] { "ActividadCodigo", "Version" });

            migrationBuilder.CreateIndex(
                name: "UQ_ProyectoActividad_Uid",
                schema: "dbo",
                table: "ProyectoActividad",
                column: "MovimientoUid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_ProyectoActividad_Version",
                schema: "dbo",
                table: "ProyectoActividad",
                columns: new[] { "ProyectoId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ProyectoActividad_LegacyId",
                schema: "dbo",
                table: "ProyectoActividad",
                column: "LegacyId",
                unique: true,
                filter: "[LegacyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProyectoAsignacionDia_Cronograma",
                schema: "dbo",
                table: "ProyectoAsignacionDia",
                column: "Fecha")
                .Annotation("SqlServer:Include", new[] { "ProyectoId", "EmpleadoId", "RolAsignacionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProyectoAsignacionDia_Cruces",
                schema: "dbo",
                table: "ProyectoAsignacionDia",
                columns: new[] { "EmpleadoId", "Fecha" })
                .Annotation("SqlServer:Include", new[] { "ProyectoId", "RolAsignacionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProyectoAsignacionDia_Personal",
                schema: "dbo",
                table: "ProyectoAsignacionDia",
                column: "ProyectoPersonalId");

            migrationBuilder.CreateIndex(
                name: "UQ_ProyectoAsignacionDia_Clave",
                schema: "dbo",
                table: "ProyectoAsignacionDia",
                columns: new[] { "ProyectoId", "EmpleadoId", "Fecha", "RolAsignacionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ProyectoAsignacionDia_LegacyId",
                schema: "dbo",
                table: "ProyectoAsignacionDia",
                column: "LegacyId",
                unique: true,
                filter: "[LegacyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_ProyectoEtapa_Uid",
                schema: "dbo",
                table: "ProyectoEtapa",
                column: "EtapaUid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_ProyectoEtapa_Version",
                schema: "dbo",
                table: "ProyectoEtapa",
                columns: new[] { "ProyectoId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ProyectoEtapa_LegacyId",
                schema: "dbo",
                table: "ProyectoEtapa",
                column: "LegacyId",
                unique: true,
                filter: "[LegacyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProyectoPersonal_Empleado",
                schema: "dbo",
                table: "ProyectoPersonal",
                column: "EmpleadoId")
                .Annotation("SqlServer:Include", new[] { "ProyectoId", "FechaInicio", "FechaFin" });

            migrationBuilder.CreateIndex(
                name: "UQ_ProyectoPersonal_Numero",
                schema: "dbo",
                table: "ProyectoPersonal",
                columns: new[] { "ProyectoId", "RolAsignacionId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ProyectoPersonal_LegacyId",
                schema: "dbo",
                table: "ProyectoPersonal",
                column: "LegacyId",
                unique: true,
                filter: "[LegacyId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_RolAsignacion_Codigo",
                schema: "dbo",
                table: "RolAsignacion",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_TipoAplicacionNovedad_Codigo",
                schema: "dbo",
                table: "TipoAplicacionNovedad",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_TipoMovimiento_Codigo",
                schema: "dbo",
                table: "TipoMovimiento",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_TipoNovedad_Codigo",
                schema: "dbo",
                table: "TipoNovedad",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Usuario_Email",
                schema: "dbo",
                table: "Usuario",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_Usuario_EntraObjectId",
                schema: "dbo",
                table: "Usuario",
                column: "EntraObjectId",
                unique: true,
                filter: "[EntraObjectId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UQ_UsuarioDepartamento",
                schema: "dbo",
                table: "UsuarioDepartamento",
                columns: new[] { "UsuarioId", "DepartamentoId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CargoInfor_CreadoPor",
                schema: "dbo",
                table: "CargoInfor",
                column: "CreadoPorId",
                principalSchema: "dbo",
                principalTable: "Usuario",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CargoInfor_ModificadoPor",
                schema: "dbo",
                table: "CargoInfor",
                column: "ModificadoPorId",
                principalSchema: "dbo",
                principalTable: "Usuario",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Compania_CreadoPor",
                schema: "dbo",
                table: "Compania",
                column: "CreadoPorId",
                principalSchema: "dbo",
                principalTable: "Usuario",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Compania_ModificadoPor",
                schema: "dbo",
                table: "Compania",
                column: "ModificadoPorId",
                principalSchema: "dbo",
                principalTable: "Usuario",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Departamento_CreadoPor",
                schema: "dbo",
                table: "Departamento",
                column: "CreadoPorId",
                principalSchema: "dbo",
                principalTable: "Usuario",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Departamento_ModificadoPor",
                schema: "dbo",
                table: "Departamento",
                column: "ModificadoPorId",
                principalSchema: "dbo",
                principalTable: "Usuario",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Empleado_CreadoPor",
                schema: "dbo",
                table: "Empleado",
                column: "CreadoPorId",
                principalSchema: "dbo",
                principalTable: "Usuario",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Empleado_ModificadoPor",
                schema: "dbo",
                table: "Empleado",
                column: "ModificadoPorId",
                principalSchema: "dbo",
                principalTable: "Usuario",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Empleado_CreadoPor",
                schema: "dbo",
                table: "Empleado");

            migrationBuilder.DropForeignKey(
                name: "FK_Empleado_ModificadoPor",
                schema: "dbo",
                table: "Empleado");

            migrationBuilder.DropTable(
                name: "CargoInfor",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "NovedadDia",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Parametro",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ProyectoActividad",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ProyectoAsignacionDia",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ProyectoEtapa",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "UsuarioDepartamento",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Novedad",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ProyectoPersonal",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TipoMovimiento",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OrigenNovedad",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TipoAplicacionNovedad",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TipoNovedad",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Jornada",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Proyecto",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RolAsignacion",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Compania",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Departamento",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "EstadoProyecto",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "GrupoProyecto",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Usuario",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Empleado",
                schema: "dbo");
        }
    }
}
