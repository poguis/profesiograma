using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class ParametroExigePrincipal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "dbo",
                table: "Parametro",
                columns: new[] { "Id", "Clave", "CreadoPorId", "Descripcion", "FechaCreacion", "FechaModificacion", "ModificadoPorId", "TipoDato", "Valor" },
                values: new object[] { 10, "PROYECTO_EXIGE_PRINCIPAL", 1, "1 = creación, actualización de personal y reactivación exigen al menos 1 principal (C10); 0 = basta 1 persona (principal o back)", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, null, "BOOL", "0" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "dbo",
                table: "Parametro",
                keyColumn: "Id",
                keyValue: 10);
        }
    }
}
