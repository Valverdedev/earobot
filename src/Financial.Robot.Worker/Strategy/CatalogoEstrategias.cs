using Financial.Robot.Domain.ValueObjects;

namespace Financial.Robot.Worker.Strategy;

/// <summary>
/// Catálogo de estratégias registradas por nome.
/// Resolve IEstrategiaEntrada a partir do nome declarado em EstrategiaConfig.Nome.
/// </summary>
public sealed class CatalogoEstrategias
{
    private readonly Dictionary<string, IEstrategiaEntrada> _catalogo = new(StringComparer.OrdinalIgnoreCase);

    public void Registrar(IEstrategiaEntrada estrategia)
    {
        _catalogo[estrategia.Nome] = estrategia;
    }

    public IEstrategiaEntrada? Resolver(string nome) =>
        _catalogo.TryGetValue(nome, out var estrategia) ? estrategia : null;
}
