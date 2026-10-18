using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class ParametroHorariosNoLaborales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "dbo",
                table: "Parametro",
                columns: new[] { "Id", "Clave", "CreadoPorId", "Descripcion", "FechaCreacion", "FechaModificacion", "ModificadoPorId", "TipoDato", "Valor" },
                values: new object[] { 11, "HORARIOS_NO_LABORALES", 1, "Descripciones de horarios del ERP que no se pueden elegir (sin distinguir mayúsculas ni tildes)", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "LIST", "LIBRE,FERIADO,VACIO,VACACIONES,PERMISO MEDICO,CALAMIDAD DOMESTICA,PATERNIDAD PROYECTOS,PATERNIDAD PLANTA" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Parametro",
                keyColumn: "Id",
                keyValue: 11);
        }
    }
}
