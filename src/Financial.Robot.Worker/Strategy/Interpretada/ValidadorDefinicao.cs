namespace Financial.Robot.Worker.Strategy.Interpretada;

public sealed class ValidadorDefinicao
{
    private static readonly HashSet<string> FiltrosConhecidos = ["spreadMaximo", "atrMinimo", "atrMaximo", "candleMaxAtrMultiplo", "janelaHorario", "semClimax"];
    private static readonly HashSet<string> OperadoresConhecidos = [">", ">=", "<", "<=", "==", "entre", "cruzouAcima", "cruzouAbaixo", "pertoDe", "distanciaMinima", "distanciaMaxima", "alinhados", "inclinacao"];
    private static readonly HashSet<string> PadroesConhecidos = ["trendBarAlta", "trendBarBaixa", "rejeicaoCompradora", "rejeicaoVendedora", "fechamentoDirecional", "sequencia", "insideBar", "outsideBar", "engolfoAlta", "engolfoBaixa", "impulsoPullback", "rompimentoReteste", "tocouNivel"];
    private static readonly HashSet<string> StopsConhecidos = ["extremoCandle", "valorOperando", "extremoRef", "nivelProximo"];

    public static IReadOnlyList<string> Validar(DefinicaoEstrategia def)
    {
        var erros = new List<string>();

        if (def.SchemaVersion != "1.0")
        {
            erros.Add("O arquivo deve especificar '$schemaVersion': '1.0'.");
        }

        if (def.Compra is null && def.Venda is null)
        {
            erros.Add("A estratégia deve definir ao menos um bloco 'compra' ou 'venda'.");
        }

        if (def.Filtros is not null)
        {
            for (int i = 0; i < def.Filtros.Count; i++)
            {
                ValidarFiltro(def.Filtros[i], $"filtros[{i}]", erros);
            }
        }

        if (def.Compra is not null) ValidarBlocoLado(def.Compra, "compra", erros);
        if (def.Venda is not null) ValidarBlocoLado(def.Venda, "venda", erros);

        if (def.Saida is not null)
        {
            if (def.Saida.Compra is null && def.Saida.Venda is null)
            {
                erros.Add("bloco saida: vazio, deve declarar 'compra' e/ou 'venda'.");
            }
            var refsDummy = new HashSet<string>();
            if (def.Saida.Compra is not null) ValidarCondicao(def.Saida.Compra, "saida.compra", 1, erros, refsDummy, isSaida: true);
            if (def.Saida.Venda is not null) ValidarCondicao(def.Saida.Venda, "saida.venda", 1, erros, refsDummy, isSaida: true);
        }

        return erros;
    }

    private static void ValidarFiltro(FiltroDef f, string path, List<string> erros)
    {
        if (string.IsNullOrWhiteSpace(f.Tipo))
        {
            erros.Add($"{path}: O filtro deve especificar 'tipo'.");
            return;
        }

        if (!FiltrosConhecidos.Contains(f.Tipo))
        {
            erros.Add($"{path}: Tipo de filtro desconhecido: '{f.Tipo}'.");
            return;
        }

        if (f.Tipo is "spreadMaximo" or "atrMinimo" or "atrMaximo")
        {
            if (f.ValorPreco is null) erros.Add($"{path} ({f.Tipo}): 'valorPreco' é obrigatório.");
            if (f.Tipo is "atrMinimo" or "atrMaximo" && f.Periodo is null)
                erros.Add($"{path} ({f.Tipo}): 'periodo' é obrigatório.");
        }
        else if (f.Tipo is "candleMaxAtrMultiplo")
        {
            if (f.Periodo is null || f.Multiplo is null)
                erros.Add($"{path} ({f.Tipo}): 'periodo' e 'multiplo' são obrigatórios.");
        }
        else if (f.Tipo is "janelaHorario")
        {
            if (string.IsNullOrWhiteSpace(f.Inicio) || string.IsNullOrWhiteSpace(f.Fim))
                erros.Add($"{path} ({f.Tipo}): 'inicio' e 'fim' são obrigatórios.");
        }
        else if (f.Tipo is "semClimax")
        {
            if (f.JanelaCandles is null || f.MultiploRange is null || f.LookbackMedioRange is null)
                erros.Add($"{path} ({f.Tipo}): 'janelaCandles', 'multiploRange' e 'lookbackMedioRange' são obrigatórios.");
        }
    }

    private static void ValidarBlocoLado(BlocoLado bloco, string path, List<string> erros)
    {
        var refsDeclaradas = new HashSet<string>();
        if (bloco.Setup is not null) ValidarCondicao(bloco.Setup, $"{path}.setup", 1, erros, refsDeclaradas, isSaida: false);
        if (bloco.Gatilho is not null) ValidarCondicao(bloco.Gatilho, $"{path}.gatilho", 1, erros, refsDeclaradas, isSaida: false);
        if (bloco.Stop is not null) ValidarStop(bloco.Stop, $"{path}.stop", erros, refsDeclaradas);
    }

    private static void ValidarStop(StopDef stop, string path, List<string> erros, HashSet<string> refsDeclaradas)
    {
        if (string.IsNullOrWhiteSpace(stop.Tipo))
        {
            erros.Add($"{path}: O stop deve especificar 'tipo'.");
            return;
        }
        
        if (!StopsConhecidos.Contains(stop.Tipo))
        {
            erros.Add($"{path}: Tipo de stop desconhecido: '{stop.Tipo}'.");
            return;
        }
        
        if (stop.BufferPreco is null)
        {
            erros.Add($"{path}: 'bufferPreco' é obrigatório.");
        }

        if (stop.Tipo == "extremoCandle")
        {
            if (stop.Candle is null || string.IsNullOrWhiteSpace(stop.Lado))
                erros.Add($"{path} (extremoCandle): 'candle' e 'lado' ('minimo' ou 'maximo') são obrigatórios.");
            else if (stop.Candle < 0)
                erros.Add($"{path} (extremoCandle): 'candle' não pode ser negativo.");
        }
        else if (stop.Tipo == "valorOperando")
        {
            if (string.IsNullOrWhiteSpace(stop.A))
                erros.Add($"{path} (valorOperando): Operando 'a' é obrigatório.");
            else
                ValidarOperandoString(stop.A, $"{path}.a", erros, isSaida: false);
        }
        else if (stop.Tipo == "extremoRef")
        {
            if (string.IsNullOrWhiteSpace(stop.Ref))
                erros.Add($"{path} (extremoRef): 'ref' é obrigatório.");
            else if (!refsDeclaradas.Contains(stop.Ref))
                erros.Add($"{path} (extremoRef): A referência '{stop.Ref}' não foi declarada em nenhuma condição do bloco.");
                
            if (string.IsNullOrWhiteSpace(stop.Lado) || (stop.Lado != "minimo" && stop.Lado != "maximo"))
                erros.Add($"{path} (extremoRef): 'lado' ('minimo' ou 'maximo') é obrigatório.");
        }
    }

    private static void ValidarCondicao(CondicaoDef c, string path, int depth, List<string> erros, HashSet<string> refsDeclaradas, bool isSaida)
    {
        if (depth > 3) 
        {
            erros.Add($"{path}: Aninhamento lógico excede o limite de 2 níveis.");
            return;
        }

        int countTipos = 0;
        if (c.Todas is not null) countTipos++;
        if (c.Qualquer is not null) countTipos++;
        if (c.Op is not null) countTipos++;
        if (c.Padrao is not null) countTipos++;

        if (countTipos == 0)
        {
            erros.Add($"{path}: Condição vazia. Deve especificar 'todas', 'qualquer', 'op' ou 'padrao'.");
            return;
        }
        
        if (countTipos > 1)
        {
            erros.Add($"{path}: Condição ambígua. Especifique apenas um entre 'todas', 'qualquer', 'op' ou 'padrao'.");
            return;
        }

        if (c.Todas is not null)
        {
            for (int i = 0; i < c.Todas.Count; i++)
                ValidarCondicao(c.Todas[i], $"{path}.todas[{i}]", depth + 1, erros, refsDeclaradas, isSaida);
            return;
        }

        if (c.Qualquer is not null)
        {
            for (int i = 0; i < c.Qualquer.Count; i++)
                ValidarCondicao(c.Qualquer[i], $"{path}.qualquer[{i}]", depth + 1, erros, refsDeclaradas, isSaida);
            return;
        }

        if (c.Op is not null)
        {
            if (!OperadoresConhecidos.Contains(c.Op))
            {
                erros.Add($"{path}: Operador desconhecido: '{c.Op}'.");
                return;
            }

            if (c.Op is "alinhados")
            {
                if (c.Serie is null || c.Serie.Count < 2)
                    erros.Add($"{path} (alinhados): 'serie' deve ser um array com pelo menos 2 operandos.");
                else
                {
                    for (int i = 0; i < c.Serie.Count; i++)
                        ValidarOperandoString(c.Serie[i], $"{path}.serie[{i}]", erros, isSaida);
                }

                if (c.Ordem != "crescente" && c.Ordem != "decrescente")
                    erros.Add($"{path} (alinhados): 'ordem' ('crescente' ou 'decrescente') é obrigatório.");
            }
            else if (c.Op is "inclinacao")
            {
                if (string.IsNullOrWhiteSpace(c.A))
                    erros.Add($"{path} (inclinacao): Operando 'a' é obrigatório.");
                else
                    ValidarOperandoString(c.A, $"{path}.a", erros, isSaida);
                
                if (c.Candles is null || string.IsNullOrWhiteSpace(c.Direcao))
                    erros.Add($"{path} (inclinacao): 'candles' e 'direcao' ('alta' ou 'baixa') são obrigatórios.");
            }
            else if (c.Op is "entre")
            {
                if (string.IsNullOrWhiteSpace(c.A) || string.IsNullOrWhiteSpace(c.Min) || string.IsNullOrWhiteSpace(c.Max))
                    erros.Add($"{path} (entre): 'a', 'min' e 'max' são obrigatórios.");
                else
                {
                    ValidarOperandoString(c.A, $"{path}.a", erros, isSaida);
                    ValidarOperandoString(c.Min, $"{path}.min", erros, isSaida);
                    ValidarOperandoString(c.Max, $"{path}.max", erros, isSaida);
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(c.A) || string.IsNullOrWhiteSpace(c.B))
                    erros.Add($"{path} ({c.Op}): Operandos 'a' e 'b' são obrigatórios.");
                else
                {
                    ValidarOperandoString(c.A, $"{path}.a", erros, isSaida);
                    ValidarOperandoString(c.B, $"{path}.b", erros, isSaida);
                }

                if (c.Op is "pertoDe" && c.ToleranciaPreco is null)
                    erros.Add($"{path} (pertoDe): 'toleranciaPreco' é obrigatório.");
                
                if (c.Op is "distanciaMinima" or "distanciaMaxima" && c.ValorPreco is null)
                    erros.Add($"{path} ({c.Op}): 'valorPreco' é obrigatório.");
            }
            return;
        }

        if (c.Padrao is not null)
        {
            if (!PadroesConhecidos.Contains(c.Padrao))
            {
                erros.Add($"{path}: Padrão de candle desconhecido: '{c.Padrao}'.");
                return;
            }

            if (c.Padrao == "sequencia")
            {
                if (string.IsNullOrWhiteSpace(c.Lado) || c.Candles is null)
                {
                    erros.Add($"{path} (sequencia): 'lado' ('alta' ou 'baixa') e 'candles' são obrigatórios.");
                }
            }
            else if (c.Padrao == "impulsoPullback")
            {
                if (string.IsNullOrWhiteSpace(c.Lado) || (c.Lado != "alta" && c.Lado != "baixa"))
                    erros.Add($"{path} (impulsoPullback): 'lado' ('alta' ou 'baixa') é obrigatório.");
            }
            else if (c.Padrao == "rompimentoReteste")
            {
                if (string.IsNullOrWhiteSpace(c.Lado) || (c.Lado != "alta" && c.Lado != "baixa"))
                    erros.Add($"{path} (rompimentoReteste): 'lado' ('alta' ou 'baixa') é obrigatório.");
                if (string.IsNullOrWhiteSpace(c.TipoNivel) || (c.TipoNivel != "suporte" && c.TipoNivel != "resistencia"))
                    erros.Add($"{path} (rompimentoReteste): 'tipoNivel' ('suporte' ou 'resistencia') é obrigatório.");
            }
            else if (c.Padrao == "tocouNivel")
            {
                if (string.IsNullOrWhiteSpace(c.TipoNivel) || (c.TipoNivel != "suporte" && c.TipoNivel != "resistencia"))
                    erros.Add($"{path} (tocouNivel): 'tipoNivel' ('suporte' ou 'resistencia') é obrigatório.");
            }
            else
            {
                if (c.Candle is null)
                {
                    erros.Add($"{path} ({c.Padrao}): 'candle' é obrigatório.");
                }
                else if (c.Candle < 0)
                {
                    erros.Add($"{path} ({c.Padrao}): 'candle' não pode ser negativo.");
                }
            }
            
            if (!string.IsNullOrWhiteSpace(c.IdRef))
            {
                if (isSaida)
                {
                    erros.Add($"{path}: Exportação de ref ('idRef') não é permitida no bloco 'saida' (avaliação independente).");
                }
                else
                {
                    refsDeclaradas.Add(c.IdRef);
                }
            }
        }
    }

    private static void ValidarOperandoString(string texto, string path, List<string> erros, bool isSaida)
    {
        if (!ParserOperando.TryParse(texto, out var operando, out var parseErro))
        {
            erros.Add($"{path}: {parseErro}");
        }
        else if (!isSaida && (operando.Fonte == TipoFonte.PosicaoPrecoEntrada || operando.Fonte == TipoFonte.PosicaoLucroBruto || operando.Fonte == TipoFonte.PosicaoLucroPercentualPreco))
        {
            erros.Add($"{path}: Operando '{texto}' só é permitido no bloco 'saida'.");
        }
        else if (isSaida && operando.Fonte == TipoFonte.Ref)
        {
            erros.Add($"{path}: Operando 'ref.*' não é permitido no bloco 'saida'.");
        }
        else if (isSaida && (operando.Fonte == TipoFonte.TickBid || operando.Fonte == TipoFonte.TickAsk || operando.Fonte == TipoFonte.TickSpread))
        {
            erros.Add($"{path}: Operando 'tick.*' não é permitido no bloco 'saida' porque o contexto não suporta variação intra-candle para fechamento de sinal.");
        }
    }
}
