---
name: mini-indice-config-candidato
description: Preparacao de config JSON candidato para Mini Indice/WIN. Use ao transformar decisoes dos comites em alteracoes minimas de config, preservando campos desconhecidos, estrategia registrada, limites, magic numbers, operar=false por padrao e rastreabilidade da tese.
---

# Mini Indice Config Candidato

## Objetivo

Preparar um config candidato para WIN a partir das conclusoes dos comites. A skill nao deve promover o arquivo ativo nem acionar hot reload.

## Leitura obrigatoria

Ler:

- config ativo do simbolo alvo;
- catalogo de estrategias;
- relatorio do comite que motivou o ajuste;
- estado de conta/terminal quando disponivel.

## Metodo

1. Partir do config ativo e alterar o minimo.
2. Preservar campos desconhecidos, magic numbers e limites existentes.
3. Escolher somente estrategia registrada.
4. Manter `operar=false` por padrao, salvo autorizacao explicita e gates de promocao.
5. Incluir metadados de rastreabilidade quando o schema permitir ou registrar no relatorio lateral.
6. Validar JSON e coerencia de unidades de preco.

## Saida esperada

```json
{
  "skill": "mini-indice-config-candidato",
  "candidato_path": "",
  "base_config_path": "",
  "alteracoes": [],
  "estrategia": "",
  "operar": false,
  "validacoes": [],
  "bloqueios_promocao": []
}
```

## Regras

- Nao editar config ativo diretamente.
- Nao promover para `D:\\SistemEarobot\\config` nesta skill.
- Se unidade de SL/TP, tick, digits ou stops level estiver incerta, bloquear promocao.
