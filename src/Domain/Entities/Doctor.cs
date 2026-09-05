namespace botFacturacion.Domain.Entities;

/// <summary>
/// Representa a un médico del consultorio.
/// </summary>
public class Doctor
{
    public int Id { get; set; }

    /// <summary>Nombre completo del doctor.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Especialidad médica (ej. Medicina General, Pediatría).</summary>
    public string Especialidad { get; set; } = string.Empty;

    /// <summary>Indica si el doctor está activo para recibir citas.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Citas asociadas a este doctor.</summary>
    public ICollection<Cita> Citas { get; set; } = new List<Cita>();
}
