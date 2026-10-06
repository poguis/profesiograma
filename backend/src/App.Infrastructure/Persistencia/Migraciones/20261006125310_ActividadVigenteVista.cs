using App.Infrastructure.Persistencia.Vistas;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Infrastructure.Persistencia.Migraciones
{
    /// <summary>
    /// TAREA-18b (O3): vwProyectoResumen con la regla común de actividad vigente (VistasSql.VwProyectoResumen_V2).
    /// Sin cambio de modelo. Down vuelve a la V1.
    /// </summary>
    public partial class ActividadVigenteVista : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"EXEC(N'{VistasSql.VwProyectoResumen_V2.Replace("'", "''")}');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"EXEC(N'{VistasSql.VwProyectoResumen_V1.Replace("'", "''")}');");
        }
    }
}
