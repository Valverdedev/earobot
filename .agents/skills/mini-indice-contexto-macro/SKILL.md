---
name: mini-indice-contexto-macro
description: Analise macro e comportamental para Mini Indice/WIN antes ou durante o pregao. Use ao avaliar impacto de exterior, dolar, curva DI, Selic, fiscal, inflacao, commodities, acoes de maior peso, agenda, politica, eleicoes, falas e noticias sobre o vies intradiario do WIN.
---

# Mini Indice Contexto Macro

## Objetivo

Produzir uma leitura macro rastreavel para o Mini Indice, separando fatos confirmados, inferencias e cenarios. A skill deve apoiar principalmente o comite pre-abertura, mas tambem pode revalidar a tese ao meio-dia.

## Fontes obrigatorias

Priorizar fontes primarias e atuais. Quando usar noticia, registrar URL, data de publicacao/evento e horario de acesso.

Consultar, quando material:

- agenda economica do Brasil, EUA e China;
- USDBRL/WDO, DXY, Treasury yields, VIX, S&P 500 e Nasdaq;
- curva DI, Selic, Copom, Focus, inflacao e fiscal;
- Petrobras/Brent, Vale/minerio, bancos e acoes de maior peso do Ibovespa;
- noticias e falas politicas com canal economico claro;
- pesquisas eleitorais somente com registro, metodologia, amostra, campo e margem.

## Metodo

1. Identificar catalisadores do dia e separar pre-mercado, intradiario e pos-pregao.
2. Classificar impacto em `altista`, `baixista`, `misto` ou `neutro` para WIN.
3. Estimar se o fator e dominante ou apenas contexto.
4. Indicar janelas de risco: abertura, dados macro, falas, leiloes, Nova York, fechamento.
5. Marcar qualquer dado nao confirmado como `[NAO CONFIRMADO]`.

## Saida esperada

Retornar um bloco objetivo:

```json
{
  "skill": "mini-indice-contexto-macro",
  "vies_macro": "altista|baixista|misto|neutro|evitar",
  "fatores_dominantes": [],
  "fatores_contraditorios": [],
  "eventos_criticos": [],
  "janelas_de_risco": [],
  "nivel_confianca": 0.0,
  "fontes": []
}
```

## Regras

- Nao inventar dados de mercado, agenda, pesquisas, falas ou consenso.
- Nao transformar correlacao em causalidade.
- Nao recomendar ordem; recomendar apenas contexto e restricoes.
