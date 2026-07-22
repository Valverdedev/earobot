namespace Financial.Robot.Domain.Enums;

/// <summary>Tipo de ordem pendente suportado pelo MT5.</summary>
public enum TipoOrdemPendente
{
    /// <summary>Compra a preço menor ou igual ao preço definido.</summary>
    CompraLimite,

    /// <summary>Venda a preço maior ou igual ao preço definido.</summary>
    VendaLimite,

    /// <summary>Compra quando o preço atingir (acima) o preço definido.</summary>
    CompraStop,

    /// <summary>Venda quando o preço atingir (abaixo) o preço definido.</summary>
    VendaStop
}
