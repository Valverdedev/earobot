namespace Financial.Robot.Domain.Enums;

/// <summary>Status de uma ordem pendente no MT5.</summary>
public enum StatusOrdemPendente
{
    /// <summary>Ordem aguardando ser ativada pelo preço.</summary>
    Pendente,

    /// <summary>Ordem cancelada manualmente.</summary>
    Cancelada,

    /// <summary>Ordem expirada pelo tempo ou condição.</summary>
    Expirada
}
