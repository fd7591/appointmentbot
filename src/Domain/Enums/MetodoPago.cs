namespace botFacturacion.Domain.Enums;

/// <summary>
/// Claves de método de pago según el catálogo del SAT para CFDI 4.0.
/// </summary>
public enum MetodoPago
{
    /// <summary>Efectivo</summary>
    Efectivo = 1,

    /// <summary>Cheque nominativo</summary>
    ChequeNominativo = 2,

    /// <summary>Transferencia electrónica de fondos</summary>
    TransferenciaElectronica = 3,

    /// <summary>Tarjeta de crédito</summary>
    TarjetaCredito = 4,

    /// <summary>Tarjeta de débito</summary>
    TarjetaDebito = 28
}

/// <summary>
/// Métodos de extensión para obtener la clave y descripción SAT de MetodoPago.
/// </summary>
public static class MetodoPagoExtensions
{
    /// <summary>Retorna la clave SAT de 2 dígitos.</summary>
    public static string GetClave(this MetodoPago metodo) => metodo switch
    {
        MetodoPago.Efectivo => "01",
        MetodoPago.ChequeNominativo => "02",
        MetodoPago.TransferenciaElectronica => "03",
        MetodoPago.TarjetaCredito => "04",
        MetodoPago.TarjetaDebito => "28",
        _ => "01"
    };

    /// <summary>Retorna la descripción completa del método de pago.</summary>
    public static string GetDescripcion(this MetodoPago metodo) => metodo switch
    {
        MetodoPago.Efectivo => "Efectivo",
        MetodoPago.ChequeNominativo => "Cheque nominativo",
        MetodoPago.TransferenciaElectronica => "Transferencia electrónica de fondos",
        MetodoPago.TarjetaCredito => "Tarjeta de crédito",
        MetodoPago.TarjetaDebito => "Tarjeta de débito",
        _ => "Efectivo"
    };

    /// <summary>
    /// Obtiene el MetodoPago a partir de su clave SAT.
    /// Retorna null si la clave no es reconocida.
    /// </summary>
    public static MetodoPago? FromClave(string clave) => clave switch
    {
        "01" => MetodoPago.Efectivo,
        "02" => MetodoPago.ChequeNominativo,
        "03" => MetodoPago.TransferenciaElectronica,
        "04" => MetodoPago.TarjetaCredito,
        "28" => MetodoPago.TarjetaDebito,
        _ => null
    };
}
