namespace botFacturacion.Domain.Enums;

/// <summary>
/// Claves de uso del CFDI según el catálogo del SAT para CFDI 4.0.
/// </summary>
public enum UsoCFDI
{
    /// <summary>Gastos en general</summary>
    GastosGeneral = 1,

    /// <summary>Honorarios médicos, dentales y hospitalarios</summary>
    HonorariosMedicos = 2,

    /// <summary>Sin efectos fiscales</summary>
    SinEfectosFiscales = 3,

    /// <summary>Por definir</summary>
    PorDefinir = 4
}

/// <summary>
/// Métodos de extensión para obtener la clave y descripción SAT de UsoCFDI.
/// </summary>
public static class UsoCFDIExtensions
{
    /// <summary>Retorna la clave SAT del uso del CFDI.</summary>
    public static string GetClave(this UsoCFDI uso) => uso switch
    {
        UsoCFDI.GastosGeneral => "G03",
        UsoCFDI.HonorariosMedicos => "D01",
        UsoCFDI.SinEfectosFiscales => "S01",
        UsoCFDI.PorDefinir => "P01",
        _ => "G03"
    };

    /// <summary>Retorna la descripción completa del uso del CFDI.</summary>
    public static string GetDescripcion(this UsoCFDI uso) => uso switch
    {
        UsoCFDI.GastosGeneral => "Gastos en general",
        UsoCFDI.HonorariosMedicos => "Honorarios médicos, dentales y hospitalarios",
        UsoCFDI.SinEfectosFiscales => "Sin efectos fiscales",
        UsoCFDI.PorDefinir => "Por definir",
        _ => "Gastos en general"
    };

    /// <summary>
    /// Obtiene el UsoCFDI a partir de su clave SAT.
    /// Retorna null si la clave no es reconocida.
    /// </summary>
    public static UsoCFDI? FromClave(string clave) => clave switch
    {
        "G03" => UsoCFDI.GastosGeneral,
        "D01" => UsoCFDI.HonorariosMedicos,
        "S01" => UsoCFDI.SinEfectosFiscales,
        "P01" => UsoCFDI.PorDefinir,
        _ => null
    };
}
