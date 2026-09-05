namespace botFacturacion.Domain.Enums;

/// <summary>
/// Claves de régimen fiscal según el catálogo del SAT para CFDI 4.0.
/// Se incluyen los regímenes más comunes para personas físicas y morales.
/// </summary>
public enum RegimenFiscal
{
    /// <summary>General de Ley Personas Morales</summary>
    GeneralPersonasMorales = 601,

    /// <summary>Personas Morales con Fines no Lucrativos</summary>
    PersonasMoralesFinesNoLucrativos = 603,

    /// <summary>Sueldos y Salarios e Ingresos Asimilados a Salarios</summary>
    SueldosYSalarios = 605,

    /// <summary>Arrendamiento</summary>
    Arrendamiento = 606,

    /// <summary>Régimen de Enajenación o Adquisición de Bienes</summary>
    EnajenacionAdquisicionBienes = 607,

    /// <summary>Demás ingresos</summary>
    DemasIngresos = 608,

    /// <summary>Residentes en el Extranjero sin Establecimiento Permanente en México</summary>
    ResidentesExtranjero = 610,

    /// <summary>Ingresos por Dividendos (socios y accionistas)</summary>
    IngresosDividendos = 611,

    /// <summary>Personas Físicas con Actividades Empresariales y Profesionales</summary>
    ActividadesEmpresarialesProfesionales = 612,

    /// <summary>Incorporación Fiscal</summary>
    IncorporacionFiscal = 621,

    /// <summary>Actividades Agrícolas, Ganaderas, Silvícolas y Pesqueras</summary>
    ActividadesAgricolasPesqueras = 622,

    /// <summary>Opcional para Grupos de Sociedades</summary>
    GruposSociedades = 623,

    /// <summary>Coordinados</summary>
    Coordinados = 624,

    /// <summary>Régimen de las Actividades Empresariales con ingresos a través de Plataformas Tecnológicas</summary>
    PlataformasTecnologicas = 625,

    /// <summary>Régimen Simplificado de Confianza — Personas Físicas</summary>
    RescoPersonasFisicas = 626,

    /// <summary>Régimen Simplificado de Confianza — Personas Morales</summary>
    RescoPersonasMorales = 628
}

/// <summary>
/// Métodos de extensión para obtener la clave y descripción SAT de RegimenFiscal.
/// </summary>
public static class RegimenFiscalExtensions
{
    /// <summary>Retorna la clave numérica SAT como cadena.</summary>
    public static string GetClave(this RegimenFiscal regimen) => ((int)regimen).ToString();

    /// <summary>Retorna la descripción completa del régimen fiscal.</summary>
    public static string GetDescripcion(this RegimenFiscal regimen) => regimen switch
    {
        RegimenFiscal.GeneralPersonasMorales => "General de Ley Personas Morales",
        RegimenFiscal.PersonasMoralesFinesNoLucrativos => "Personas Morales con Fines no Lucrativos",
        RegimenFiscal.SueldosYSalarios => "Sueldos y Salarios e Ingresos Asimilados a Salarios",
        RegimenFiscal.Arrendamiento => "Arrendamiento",
        RegimenFiscal.EnajenacionAdquisicionBienes => "Régimen de Enajenación o Adquisición de Bienes",
        RegimenFiscal.DemasIngresos => "Demás ingresos",
        RegimenFiscal.ResidentesExtranjero => "Residentes en el Extranjero sin Establecimiento Permanente en México",
        RegimenFiscal.IngresosDividendos => "Ingresos por Dividendos (socios y accionistas)",
        RegimenFiscal.ActividadesEmpresarialesProfesionales => "Personas Físicas con Actividades Empresariales y Profesionales",
        RegimenFiscal.IncorporacionFiscal => "Incorporación Fiscal",
        RegimenFiscal.ActividadesAgricolasPesqueras => "Actividades Agrícolas, Ganaderas, Silvícolas y Pesqueras",
        RegimenFiscal.GruposSociedades => "Opcional para Grupos de Sociedades",
        RegimenFiscal.Coordinados => "Coordinados",
        RegimenFiscal.PlataformasTecnologicas => "Actividades Empresariales con ingresos vía Plataformas Tecnológicas",
        RegimenFiscal.RescoPersonasFisicas => "Régimen Simplificado de Confianza (Personas Físicas)",
        RegimenFiscal.RescoPersonasMorales => "Régimen Simplificado de Confianza (Personas Morales)",
        _ => "Sin especificar"
    };

    /// <summary>
    /// Obtiene el RegimenFiscal a partir de su clave numérica como cadena.
    /// Retorna null si la clave no es reconocida.
    /// </summary>
    public static RegimenFiscal? FromClave(string clave)
    {
        if (int.TryParse(clave, out var valor) && Enum.IsDefined(typeof(RegimenFiscal), valor))
            return (RegimenFiscal)valor;
        return null;
    }

    /// <summary>
    /// Lista de regímenes comunes para personas físicas, ideal para mostrar en el bot.
    /// </summary>
    public static IEnumerable<RegimenFiscal> CommonesFisica =>
    [
        RegimenFiscal.SueldosYSalarios,
        RegimenFiscal.Arrendamiento,
        RegimenFiscal.ActividadesEmpresarialesProfesionales,
        RegimenFiscal.IncorporacionFiscal,
        RegimenFiscal.RescoPersonasFisicas
    ];

    /// <summary>
    /// Lista de regímenes comunes para personas morales, ideal para mostrar en el bot.
    /// </summary>
    public static IEnumerable<RegimenFiscal> CommonesMoral =>
    [
        RegimenFiscal.GeneralPersonasMorales,
        RegimenFiscal.PersonasMoralesFinesNoLucrativos,
        RegimenFiscal.RescoPersonasMorales
    ];
}
