# Regras Globais do Projeto - financial.robot

## Idioma
- Toda comunicação com o usuário deve ser em **Português (Brasil)**.
- Código, variáveis, classes, métodos e comentários técnicos devem ser escritos em **Portugues**.
- Documentação de usuário (README, CHANGELOG) pode ser bilíngue.

## Tecnologia
- **Plataforma**: .NET 8+ (C#)
- **Arquitetura**: Domain-Driven Design (DDD) com Clean Architecture
- **Princípios**: SOLID, Clean Code, KISS, DRY, YAGNI
- **Integração**: MetaTrader 5 (MT5) via API/Pipes/Sockets

## Padrões de Código
- Usar `async/await` sempre que possível para operações I/O
- Usar nullable reference types (`#nullable enable`)
- Seguir convenções de nomenclatura da Microsoft para C#
- Toda classe pública deve ter XML Documentation Comments
- Métodos não devem exceder 20 linhas; classes não devem exceder 200 linhas
- Injeção de dependência via constructor injection
- Usar `record` para Value Objects
- Usar `sealed` em classes que não devem ser herdadas
- Exceptions customizadas devem herdar de `DomainException`

## Testes
- Todo código de domínio deve ter testes unitários
- Usar xUnit como framework de testes
- Usar FluentAssertions para asserções
- Usar NSubstitute para mocks
- Nomenclatura: `MetodoSobTeste_Cenario_ResultadoEsperado`

## Git
- Commits seguem Conventional Commits (feat, fix, refactor, test, docs, chore)
- Uma branch por feature/fix
- Pull Requests obrigatórios para `main`

## Segurança
- Nunca hardcode de credenciais, senhas ou API keys
- Usar `IOptions<T>` pattern para configurações
- Secrets via User Secrets (dev) ou Azure Key Vault (prod)
