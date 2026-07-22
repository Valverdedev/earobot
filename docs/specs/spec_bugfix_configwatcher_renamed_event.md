# Bugfix — ConfigWatcher não reconhece arquivos escritos via rename atômico

> Investigado a pedido do usuário: "por que o worker não reconhece quando coloco [um config] lá [na pasta D:\SistemEarobot\config]" — mas reconhece quando o usuário abre o arquivo manualmente e dá save.

## Sintoma observado

Arquivos `.config.json` gravados na pasta `D:\SistemEarobot\config` por uma ferramenta externa (agente/automação) não disparam o log `Novo config carregado para {Symbol}` nem `Config atualizado para {Symbol}` — o `StrategyEngine` correspondente nunca inicia, mesmo com o conteúdo do arquivo válido e presente no disco. Quando o mesmo arquivo é aberto e salvo manualmente (ex.: VS Code, Notepad), o robô reconhece a mudança imediatamente.

Também observado: reiniciar o processo (`Application started`) sempre recarrega corretamente todos os arquivos presentes na pasta no momento do boot — o problema é específico do watcher em tempo real (`FileSystemWatcher`), não do carregamento inicial (`Directory.GetFiles` no `ExecuteAsync`).

## Causa raiz

`src/Financial.Robot.Worker/Config/ConfigWatcherService.cs`, linhas 61-68:

```csharp
_watcher = new FileSystemWatcher(fullPath, "*.config.json")
{
    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
    EnableRaisingEvents = true
};

_watcher.Changed += OnFileChanged;
_watcher.Created += OnFileChanged;
```

O watcher assina apenas os eventos `Changed` e `Created`. **Não assina `Renamed`.**

Muitas ferramentas de escrita de arquivo (editores de texto, e principalmente agentes de sincronização de nuvem como OneDrive/Dropbox, que é o caso provável do drive `D:\` aqui, dado que esta pasta é acessada via uma ponte remota/sincronizada) atualizam um arquivo existente através do padrão "escrever em arquivo temporário + renomear por cima do original" (write-then-rename atômico). Esse padrão é usado justamente para evitar que outro processo leia um arquivo parcialmente escrito. Do ponto de vista do NTFS/Win32, essa operação gera um evento `Renamed` (ou uma sequência de `Created`+`Deleted` dependendo da implementação exata), **não** um `Changed` no arquivo de destino final.

Como o `ConfigWatcherService` não escuta `Renamed`, o arquivo é fisicamente atualizado no disco, mas o evento que deveria disparar `OnFileChanged` → `ProcessFileAsync` nunca chega ao código — o config fica "invisível" para o robô até o próximo restart do processo (que faz um scan completo via `Directory.GetFiles`, não depende de eventos).

Isso é agravado pelo fato de `FileSystemWatcher` no Windows ser conhecidamente não-confiável sobre pastas sincronizadas com a nuvem ou compartilhamentos de rede — pode perder eventos silenciosamente mesmo quando o handler certo está assinado, especialmente sob rajadas de mudanças (buffer interno padrão de 8KB, sem tratamento de overflow neste código).

## Correção proposta

### 1. Assinar o evento `Renamed` (correção principal, baixo risco)

```csharp
_watcher.Changed += OnFileChanged;
_watcher.Created += OnFileChanged;
_watcher.Renamed += OnFileChanged; // NOVO — cobre o padrão write-temp+rename usado por editores e sync de nuvem
```

`FileSystemEventArgs` e `RenamedEventArgs` são compatíveis (`RenamedEventArgs` herda de `FileSystemEventArgs`), então `OnFileChanged(object sender, FileSystemEventArgs e)` já aceita o novo handler sem mudança de assinatura.

### 2. Fallback de polling periódico (defesa em profundidade, recomendado)

Adicionar um `PeriodicTimer` (ex.: a cada 30-60s) que re-executa o mesmo loop de `Directory.GetFiles(fullPath, "*.config.json")` + `ProcessFileAsync` já usado no boot inicial (`ExecuteAsync`, linhas 71-77). Como `ProcessFileAsync` já é idempotente (só loga/dispara `ConfigChanged` quando o config é novo ou o conteúdo mudou, via `AddOrUpdate` no `_activeConfigs`), este polling é seguro de rodar em paralelo ao watcher — serve apenas como rede de segurança para os casos em que o `FileSystemWatcher` perde o evento por qualquer motivo (não só o caso do rename, mas também truncamento de buffer sob rajada, ou peculiaridades do provedor de sincronização do disco).

```csharp
// Dentro de ExecuteAsync, além do watcher:
_ = Task.Run(async () =>
{
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
    while (await timer.WaitForNextTickAsync(stoppingToken))
    {
        foreach (var file in Directory.GetFiles(fullPath, "*.config.json"))
        {
            await ProcessFileAsync(file);
        }
    }
});
```

## Fora de escopo

- Não investiga se `D:\` é de fato uma pasta sincronizada por nuvem (OneDrive/Dropbox/etc.) — essa é a hipótese mais provável dado o comportamento observado, mas não foi confirmada diretamente (não há acesso às configurações do Explorer/sync client da máquina do usuário a partir deste ambiente).
- Não altera a lógica de debounce (`_debounceTime = 500ms`) nem o `InternalBufferSize` do `FileSystemWatcher` — mudanças possíveis, mas não estritamente necessárias para resolver o sintoma relatado.

## Teste recomendado

1. Aplicar a correção 1 (assinar `Renamed`).
2. Gravar um arquivo `.config.json` novo/atualizado na pasta `D:\SistemEarobot\config` por fora de um editor local (ex.: via automação, script, ou copiando de outro local) e confirmar que o log `Novo config carregado`/`Config atualizado` aparece em segundos, sem precisar reiniciar o processo nem abrir o arquivo manualmente.
3. Se o sintoma persistir mesmo após a correção 1 (indicando que o provedor de sync usa um padrão de evento ainda mais atípico), aplicar a correção 2 (polling) como rede de segurança definitiva.
