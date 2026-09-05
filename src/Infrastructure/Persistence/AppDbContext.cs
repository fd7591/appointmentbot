using botFacturacion.Domain.Entities;
using botFacturacion.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace botFacturacion.Infrastructure.Persistence;

/// <summary>
/// Contexto principal de Entity Framework Core.
/// Gestiona todas las entidades del sistema de facturación y citas.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ─── DbSets ───────────────────────────────────────────────────────────────

    public DbSet<Doctor> Doctores { get; set; } = null!;
    public DbSet<Disponibilidad> Disponibilidades { get; set; } = null!;
    public DbSet<Cita> Citas { get; set; } = null!;
    public DbSet<ReceptorFiscal> ReceptoresFiscales { get; set; } = null!;
    public DbSet<SolicitudFactura> SolicitudesFactura { get; set; } = null!;
    public DbSet<ConversationState> ConversationStates { get; set; } = null!;

    // ─── Configuración del modelo ──────────────────────────────────────────────

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Doctor ────────────────────────────────────────────────────────────
        modelBuilder.Entity<Doctor>(e =>
        {
            e.ToTable("Doctores");
            e.HasKey(d => d.Id);
            e.Property(d => d.Nombre).IsRequired().HasMaxLength(200);
            e.Property(d => d.Especialidad).IsRequired().HasMaxLength(100);
        });

        // ── Disponibilidad ────────────────────────────────────────────────────
        modelBuilder.Entity<Disponibilidad>(e =>
        {
            e.ToTable("Disponibilidades");
            e.HasKey(d => d.Id);
            e.Property(d => d.DiaSemana).IsRequired();
            e.Property(d => d.HoraInicio).IsRequired();
            e.Property(d => d.HoraFin).IsRequired();
            e.Property(d => d.MaxCitas).HasDefaultValue(1);

            // Índice por día de semana para consultas de disponibilidad
            e.HasIndex(d => new { d.DiaSemana, d.Activo });
        });

        // ── Cita ──────────────────────────────────────────────────────────────
        modelBuilder.Entity<Cita>(e =>
        {
            e.ToTable("Citas");
            e.HasKey(c => c.Id);
            e.Property(c => c.PacienteNombre).IsRequired().HasMaxLength(200);
            e.Property(c => c.PacienteTelefono).IsRequired().HasMaxLength(20);
            e.Property(c => c.FolioConfirmacion).IsRequired().HasMaxLength(30);
            e.Property(c => c.PacienteTelefonoContacto).HasMaxLength(15);
            e.Property(c => c.PacienteEmail).HasMaxLength(200);
            e.Property(c => c.Motivo).IsRequired().HasMaxLength(500);
            e.Property(c => c.Estado)
                .HasConversion<string>()
                .HasMaxLength(20);
            e.Property(c => c.CreadoEn).HasDefaultValueSql("GETUTCDATE()");

            // Índices para consultas frecuentes
            e.HasIndex(c => c.PacienteTelefono);
            e.HasIndex(c => c.FolioConfirmacion).IsUnique();
            e.HasIndex(c => new { c.FechaCita, c.Estado });
            e.HasIndex(c => new { c.RecordatorioEnviado, c.FechaCita });

            // Relación con Doctor (opcional)
            e.HasOne(c => c.Doctor)
             .WithMany(d => d.Citas)
             .HasForeignKey(c => c.DoctorId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── ReceptorFiscal ────────────────────────────────────────────────────
        modelBuilder.Entity<ReceptorFiscal>(e =>
        {
            e.ToTable("ReceptoresFiscales");
            e.HasKey(r => r.Id);
            e.Property(r => r.RFC).IsRequired().HasMaxLength(13);
            e.Property(r => r.RazonSocial).IsRequired().HasMaxLength(300);
            e.Property(r => r.RegimenFiscalClave).IsRequired().HasMaxLength(5);
            e.Property(r => r.RegimenFiscalDescripcion).IsRequired().HasMaxLength(200);
            e.Property(r => r.CodigoPostal).IsRequired().HasMaxLength(5);
            e.Property(r => r.Email).IsRequired().HasMaxLength(200);
            e.Property(r => r.TelefonoRegistro).IsRequired().HasMaxLength(20);
            e.Property(r => r.CreadoEn).HasDefaultValueSql("GETUTCDATE()");
            e.Property(r => r.ActualizadoEn).HasDefaultValueSql("GETUTCDATE()");

            // RFC único en la tabla
            e.HasIndex(r => r.RFC).IsUnique();
        });

        // ── SolicitudFactura ──────────────────────────────────────────────────
        modelBuilder.Entity<SolicitudFactura>(e =>
        {
            e.ToTable("SolicitudesFactura");
            e.HasKey(s => s.Id);
            e.Property(s => s.Folio).IsRequired().HasMaxLength(30);
            e.Property(s => s.TelefonoSolicitante).IsRequired().HasMaxLength(20);
            e.Property(s => s.MetodoPagoClave).IsRequired().HasMaxLength(5);
            e.Property(s => s.MetodoPagoDescripcion).IsRequired().HasMaxLength(100);
            e.Property(s => s.FormaPago).IsRequired().HasMaxLength(3);
            e.Property(s => s.UsoCFDIClave).IsRequired().HasMaxLength(5);
            e.Property(s => s.UsoCFDIDescripcion).IsRequired().HasMaxLength(200);
            e.Property(s => s.Monto).HasColumnType("decimal(10,2)");
            e.Property(s => s.Estado)
                .HasConversion<string>()
                .HasMaxLength(20);
            e.Property(s => s.NotasContador).HasMaxLength(500);
            e.Property(s => s.FolioFiscalUUID).HasMaxLength(50);
            e.Property(s => s.CreadoEn).HasDefaultValueSql("GETUTCDATE()");
            e.Property(s => s.ActualizadoEn).HasDefaultValueSql("GETUTCDATE()");

            // Índices
            e.HasIndex(s => s.Folio).IsUnique();
            e.HasIndex(s => s.TelefonoSolicitante);
            e.HasIndex(s => s.Estado);

            // Relación con ReceptorFiscal
            e.HasOne(s => s.ReceptorFiscal)
             .WithMany(r => r.Solicitudes)
             .HasForeignKey(s => s.ReceptorFiscalId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── ConversationState ─────────────────────────────────────────────────
        modelBuilder.Entity<ConversationState>(e =>
        {
            e.ToTable("ConversationStates");
            e.HasKey(c => c.Id);
            e.Property(c => c.PhoneNumber).IsRequired().HasMaxLength(20);
            e.Property(c => c.CurrentFlow)
                .HasConversion<string>()
                .HasMaxLength(30);
            e.Property(c => c.CurrentStep)
                .HasConversion<string>()
                .HasMaxLength(50);
            e.Property(c => c.TempData).HasColumnType("nvarchar(max)");
            e.Property(c => c.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.Property(c => c.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Índice único por número de teléfono (un estado por usuario)
            e.HasIndex(c => c.PhoneNumber).IsUnique();
            // Índice para el job de limpieza de sesiones expiradas
            e.HasIndex(c => c.UpdatedAt);
        });

        // ── Datos iniciales de disponibilidad (Lunes a Viernes, 9:00–14:00) ──
        modelBuilder.Entity<Disponibilidad>().HasData(
            // Lunes
            new Disponibilidad { Id = 1, DiaSemana = 1, HoraInicio = new TimeSpan(9, 0, 0), HoraFin = new TimeSpan(9, 30, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 2, DiaSemana = 1, HoraInicio = new TimeSpan(9, 30, 0), HoraFin = new TimeSpan(10, 0, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 3, DiaSemana = 1, HoraInicio = new TimeSpan(10, 0, 0), HoraFin = new TimeSpan(10, 30, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 4, DiaSemana = 1, HoraInicio = new TimeSpan(10, 30, 0), HoraFin = new TimeSpan(11, 0, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 5, DiaSemana = 1, HoraInicio = new TimeSpan(11, 0, 0), HoraFin = new TimeSpan(11, 30, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 6, DiaSemana = 1, HoraInicio = new TimeSpan(11, 30, 0), HoraFin = new TimeSpan(12, 0, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 7, DiaSemana = 1, HoraInicio = new TimeSpan(12, 0, 0), HoraFin = new TimeSpan(12, 30, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 8, DiaSemana = 1, HoraInicio = new TimeSpan(12, 30, 0), HoraFin = new TimeSpan(13, 0, 0), MaxCitas = 1, Activo = true },
            // Martes
            new Disponibilidad { Id = 9, DiaSemana = 2, HoraInicio = new TimeSpan(9, 0, 0), HoraFin = new TimeSpan(9, 30, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 10, DiaSemana = 2, HoraInicio = new TimeSpan(9, 30, 0), HoraFin = new TimeSpan(10, 0, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 11, DiaSemana = 2, HoraInicio = new TimeSpan(10, 0, 0), HoraFin = new TimeSpan(10, 30, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 12, DiaSemana = 2, HoraInicio = new TimeSpan(10, 30, 0), HoraFin = new TimeSpan(11, 0, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 13, DiaSemana = 2, HoraInicio = new TimeSpan(11, 0, 0), HoraFin = new TimeSpan(11, 30, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 14, DiaSemana = 2, HoraInicio = new TimeSpan(11, 30, 0), HoraFin = new TimeSpan(12, 0, 0), MaxCitas = 1, Activo = true },
            // Miércoles
            new Disponibilidad { Id = 15, DiaSemana = 3, HoraInicio = new TimeSpan(9, 0, 0), HoraFin = new TimeSpan(9, 30, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 16, DiaSemana = 3, HoraInicio = new TimeSpan(9, 30, 0), HoraFin = new TimeSpan(10, 0, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 17, DiaSemana = 3, HoraInicio = new TimeSpan(10, 0, 0), HoraFin = new TimeSpan(10, 30, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 18, DiaSemana = 3, HoraInicio = new TimeSpan(10, 30, 0), HoraFin = new TimeSpan(11, 0, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 19, DiaSemana = 3, HoraInicio = new TimeSpan(11, 0, 0), HoraFin = new TimeSpan(11, 30, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 20, DiaSemana = 3, HoraInicio = new TimeSpan(11, 30, 0), HoraFin = new TimeSpan(12, 0, 0), MaxCitas = 1, Activo = true },
            // Jueves
            new Disponibilidad { Id = 21, DiaSemana = 4, HoraInicio = new TimeSpan(9, 0, 0), HoraFin = new TimeSpan(9, 30, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 22, DiaSemana = 4, HoraInicio = new TimeSpan(9, 30, 0), HoraFin = new TimeSpan(10, 0, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 23, DiaSemana = 4, HoraInicio = new TimeSpan(10, 0, 0), HoraFin = new TimeSpan(10, 30, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 24, DiaSemana = 4, HoraInicio = new TimeSpan(10, 30, 0), HoraFin = new TimeSpan(11, 0, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 25, DiaSemana = 4, HoraInicio = new TimeSpan(11, 0, 0), HoraFin = new TimeSpan(11, 30, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 26, DiaSemana = 4, HoraInicio = new TimeSpan(11, 30, 0), HoraFin = new TimeSpan(12, 0, 0), MaxCitas = 1, Activo = true },
            // Viernes
            new Disponibilidad { Id = 27, DiaSemana = 5, HoraInicio = new TimeSpan(9, 0, 0), HoraFin = new TimeSpan(9, 30, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 28, DiaSemana = 5, HoraInicio = new TimeSpan(9, 30, 0), HoraFin = new TimeSpan(10, 0, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 29, DiaSemana = 5, HoraInicio = new TimeSpan(10, 0, 0), HoraFin = new TimeSpan(10, 30, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 30, DiaSemana = 5, HoraInicio = new TimeSpan(10, 30, 0), HoraFin = new TimeSpan(11, 0, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 31, DiaSemana = 5, HoraInicio = new TimeSpan(11, 0, 0), HoraFin = new TimeSpan(11, 30, 0), MaxCitas = 1, Activo = true },
            new Disponibilidad { Id = 32, DiaSemana = 5, HoraInicio = new TimeSpan(11, 30, 0), HoraFin = new TimeSpan(12, 0, 0), MaxCitas = 1, Activo = true }
        );
    }
}
