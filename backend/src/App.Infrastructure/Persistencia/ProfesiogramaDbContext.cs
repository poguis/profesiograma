using App.Domain.Catalogos;
using App.Domain.Configuracion;
using App.Domain.Maestros;
using App.Domain.Novedades;
using App.Domain.Proyectos;
using App.Domain.Seguridad;
using App.Infrastructure.Persistencia.Convenciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace App.Infrastructure.Persistencia;

public sealed class ProfesiogramaDbContext(DbContextOptions<ProfesiogramaDbContext> options) : DbContext(options)
{
    // Seguridad y configuración
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Departamento> Departamentos => Set<Departamento>();
    public DbSet<UsuarioDepartamento> UsuarioDepartamentos => Set<UsuarioDepartamento>();
    public DbSet<Parametro> Parametros => Set<Parametro>();
    public DbSet<CargoInfor> CargosInfor => Set<CargoInfor>();

    // Maestros (cachés ERP)
    public DbSet<Empleado> Empleados => Set<Empleado>();
    public DbSet<Compania> Companias => Set<Compania>();

    // Catálogos
    public DbSet<EstadoProyecto> EstadosProyecto => Set<EstadoProyecto>();
    public DbSet<TipoMovimiento> TiposMovimiento => Set<TipoMovimiento>();
    public DbSet<GrupoProyecto> GruposProyecto => Set<GrupoProyecto>();
    public DbSet<Jornada> Jornadas => Set<Jornada>();
    public DbSet<RolAsignacion> RolesAsignacion => Set<RolAsignacion>();
    public DbSet<TipoAplicacionNovedad> TiposAplicacionNovedad => Set<TipoAplicacionNovedad>();
    public DbSet<OrigenNovedad> OrigenesNovedad => Set<OrigenNovedad>();
    public DbSet<TipoNovedad> TiposNovedad => Set<TipoNovedad>();

    // Proyectos
    public DbSet<Proyecto> Proyectos => Set<Proyecto>();
    public DbSet<ProyectoPersonal> ProyectoPersonal => Set<ProyectoPersonal>();
    public DbSet<ProyectoAsignacionDia> ProyectoAsignacionesDia => Set<ProyectoAsignacionDia>();
    public DbSet<ProyectoEtapa> ProyectoEtapas => Set<ProyectoEtapa>();
    public DbSet<ProyectoActividad> ProyectoActividades => Set<ProyectoActividad>();

    // Novedades
    public DbSet<Novedad> Novedades => Set<Novedad>();
    public DbSet<NovedadDia> NovedadDias => Set<NovedadDia>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Sin índices automáticos por FK: los índices se declaran explícitamente,
        // igual que en docs/fases/FASE_2_modelo_profesiograma.sql.
        configurationBuilder.Conventions.Remove(typeof(ForeignKeyIndexConvention));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("dbo");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProfesiogramaDbContext).Assembly);

        // Tipos globales: marcas de tiempo DATETIME2(0) en UTC; horas TIME(0).
        foreach (var entidad in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var propiedad in entidad.GetProperties())
            {
                if (propiedad.ClrType == typeof(DateTime) || propiedad.ClrType == typeof(DateTime?))
                {
                    propiedad.SetColumnType("datetime2(0)");
                    propiedad.SetValueConverter(UtcDateTimeConverter.Instancia);
                }
                else if (propiedad.ClrType == typeof(TimeOnly) || propiedad.ClrType == typeof(TimeOnly?))
                {
                    propiedad.SetColumnType("time(0)");
                }
            }
        }
    }
}
