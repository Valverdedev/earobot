---
name: mini-indice-fluxo-e-correlatos
description: Analise de fluxo e ativos correlatos para Mini Indice/WIN. Use ao comparar WIN com dolar futuro/WDO, USDBRL, Ibovespa, Petrobras, Vale, bancos, S&P 500, Nasdaq, VIX, yields, commodities e fluxo estrangeiro para evitar operar o indice isolado.
---

# Mini Indice Fluxo e Correlatos

## Objetivo

Checar se o movimento do WIN esta confirmado ou contradito por mercados relacionados. A skill nao deve assumir correlacao fixa; deve identificar o regime atual da relacao.

## Correlatos minimos

Avaliar quando houver dados:

- WDO/USDBRL e DXY;
- S&P 500, Nasdaq, VIX e Treasury yields;
- Petrobras/Brent, Vale/minerio, bancos e Ibovespa;
- curva DI e juros locais;
- fluxo estrangeiro quando disponivel e atualizado.

## Metodo

1. Comparar direcao, intensidade e timing entre WIN e correlatos.
2. Separar confirmacao simultanea de movimento atrasado.
3. Identificar divergencias materiais: WIN sobe com dolar/juros pressionando, ou cai com exterior forte.
4. Classificar se o correlato esta liderando, acompanhando ou divergindo.
5. Produzir impacto operacional: reforca, reduz confianca ou bloqueia tese.

## Saida esperada

```json
{
  "skill": "mini-indice-fluxo-e-correlatos",
  "confirmacao_intermercado": "forte|moderada|fraca|contraditoria|indisponivel",
  "lideres_do_movimento": [],
  "divergencias": [],
  "riscos_de_leitura": [],
  "impacto_no_vies": "reforca|reduz|invalida|neutro",
  "fontes": []
}
```

## Regras

- Nao afirmar causalidade sem evento e reacao observada.
- Nao misturar frequencias diferentes sem timestamp.
- Nao usar pesos antigos do Ibovespa como se fossem carteira vigente.
