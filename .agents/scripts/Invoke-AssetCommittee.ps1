param(
    [Parameter(Mandatory = $true)]
    [string]$Ativo,

    [ValidateSet("Auto", "B3", "FOREX", "CRYPTO", "STOCK")]
    [string]$Classe = "Auto",

    [ValidateSet("configurar-estrategia", "reavaliar", "auditar", "pre-abertura", "meio-dia", "pos-pregao")]
    [string]$Objetivo = "configurar-estrategia",

    [string]$RootPath = "D:\SistemEarobot\financial.robot",

    [string]$ConfigPath = "",

    [string]$Terminal = "",

    [switch]$Execute,

    [string]$AgentCommand = "",

    [string]$ExtraContext = ""
)

$ErrorActionPreference = "Stop"

function Resolve-AssetClass {
    param([string]$Ativo, [string]$Classe)

    if ($Classe -ne "Auto") {
        return $Classe
    }

    $upper = $Ativo.ToUpperInvariant()

    if ($upper -match "^(WIN|WDO|IND|DOL|PETR|VALE|ITUB|BBDC|BBAS|BOVA|SMAL|IBOV)") {
        return "B3"
    }

    if ($upper -match "^(BTC|ETH|SOL|ADA|XRP|DOGE|BNB)" -or $upper -match "(BTC|ETH|USDT|USDC)") {
        return "CRYPTO"
    }

    if ($upper -match "^[A-Z]{6}Z?$" -and $upper -match "(USD|EUR|JPY|GBP|CHF|AUD|NZD|CAD)") {
        return "FOREX"
    }

    return "STOCK"
}

function Get-CommitteeFileName {
    param([string]$ClasseResolvida)

    switch ($ClasseResolvida) {
        "B3" { return "generic-b3-config.json" }
        "FOREX" { return "generic-forex-config.json" }
        "CRYPTO" { return "generic-crypto-config.json" }
        "STOCK" { return "generic-stock-config.json" }
        default { throw "Classe nao suportada: $ClasseResolvida" }
    }
}

function ConvertTo-HashtableById {
    param([object[]]$Items)

    $map = @{}
    foreach ($item in $Items) {
        $map[$item.id] = $item
    }
    return $map
}

function Get-TaskLayers {
    param([object[]]$Tasks)

    $remaining = @($Tasks)
    $completed = New-Object System.Collections.Generic.HashSet[string]
    $layers = New-Object System.Collections.Generic.List[object]

    while ($remaining.Count -gt 0) {
        $ready = @()
        foreach ($task in $remaining) {
            $deps = @($task.dependsOn)
            $allDone = $true
            foreach ($dep in $deps) {
                if (-not $completed.Contains([string]$dep)) {
                    $allDone = $false
                    break
                }
            }

            if ($allDone) {
                $ready += $task
            }
        }

        if ($ready.Count -eq 0) {
            throw "Dependencias circulares ou invalidas no comite."
        }

        $layers.Add($ready)
        foreach ($task in $ready) {
            [void]$completed.Add([string]$task.id)
        }

        $remaining = @($remaining | Where-Object { -not $completed.Contains([string]$_.id) })
    }

    return $layers
}

function Resolve-DefaultConfigPath {
    param([string]$Ativo, [string]$ConfigPath)

    if (-not [string]::IsNullOrWhiteSpace($ConfigPath)) {
        return $ConfigPath
    }

    $direct = "D:\SistemEarobot\config\$Ativo.config.json"
    if (Test-Path $direct) {
        return $direct
    }

    return $direct
}

function New-AgentPrompt {
    param(
        [object]$Committee,
        [object]$Agent,
        [object]$Task,
        [string]$RunPath,
        [string]$Ativo,
        [string]$ClasseResolvida,
        [string]$Objetivo,
        [string]$ResolvedConfigPath,
        [string]$Terminal,
        [string]$ExtraContext
    )

    $skillList = if ($Agent.skills.Count -gt 0) { ($Agent.skills -join ", ") } else { "usar skills locais relevantes se existirem para o ativo/classe" }
    $dependencyText = if ($Task.dependsOn.Count -gt 0) {
        ($Task.dependsOn | ForEach-Object { "- $_`: $RunPath\outputs\$_.md" }) -join [Environment]::NewLine
    } else {
        "Sem dependencias."
    }

    return @"
Voce e um agente local de comite do robo financial.robot.

Comite: $($Committee.name) - $($Committee.title)
Ativo: $Ativo
Classe: $ClasseResolvida
Objetivo: $Objetivo
Terminal preferido/informado: $Terminal
Config ativo esperado: $ResolvedConfigPath
Papel: $($Agent.role)
TaskId: $($Task.id)
Skills a usar: $skillList

Contexto principal do projeto:
- Catalogo de estrategias: $RootPath\docs\catalogo-estrategias.md
- Configs ativos: D:\SistemEarobot\config
- Relatorios: D:\SistemEarobot\relatorios
- Extrator: $RootPath\publish-extractor\Financial.Robot.Extractor.exe
- Scripts de skills: $RootPath\.agents\skills
- Saida desta run: $RunPath

Dependencias disponiveis:
$dependencyText

Contexto adicional do operador:
$ExtraContext

Tarefa:
$($Task.prompt)

Regras:
1. Escreva em Portugues do Brasil.
2. Nao invente cotacoes, noticias, candles, fontes, posicoes, PnL ou configs.
3. Separe fato, calculo, inferencia, cenario e decisao.
4. Releia o catalogo antes de recomendar estrategia.
5. Trate ambiente real/demo explicitamente quando houver dado de conta.
6. Preserve campos desconhecidos de config; nao reescreva schema por inteiro sem necessidade.
7. Nao promova config ativo nem altere D:\SistemEarobot\config nesta task isolada.
8. Config candidato deve usar operar=false por padrao, salvo autorizacao explicita no contexto.
9. Qualquer aumento de lote, drawdown, posicoes simultaneas ou permissao de operar deve ser marcado como ambiente demo/teste ou exigir autorizacao explicita.
10. Se dados forem insuficientes, bloqueie conservadoramente.

Entregavel:
Grave ou retorne o parecer da task $($Task.id) para:
$RunPath\outputs\$($Task.id).md
"@
}

$resolvedClass = Resolve-AssetClass -Ativo $Ativo -Classe $Classe
$committeeFile = Join-Path $RootPath (Join-Path ".agents\committees" (Get-CommitteeFileName -ClasseResolvida $resolvedClass))
if (-not (Test-Path $committeeFile)) {
    throw "Definicao de comite nao encontrada: $committeeFile"
}

$resolvedConfigPath = Resolve-DefaultConfigPath -Ativo $Ativo -ConfigPath $ConfigPath
$committee = Get-Content $committeeFile -Raw | ConvertFrom-Json

$runStamp = Get-Date -Format "yyyyMMdd-HHmmss"
$dateFolder = Get-Date -Format "yyyy-MM-dd"
$safeAtivo = $Ativo -replace "[^a-zA-Z0-9_.-]", "_"
$runPath = Join-Path $RootPath ".agents\runs\committees\$safeAtivo\$dateFolder\$($committee.name)-$Objetivo-$runStamp"
$promptPath = Join-Path $runPath "prompts"
$outputPath = Join-Path $runPath "outputs"

New-Item -ItemType Directory -Force -Path $promptPath, $outputPath | Out-Null

$committee | ConvertTo-Json -Depth 20 | Set-Content -Path (Join-Path $runPath "committee.json") -Encoding UTF8
@{
    runId = "$($committee.name)-$Objetivo-$runStamp"
    committee = $committee.name
    ativo = $Ativo
    classe = $resolvedClass
    objetivo = $Objetivo
    configPath = $resolvedConfigPath
    terminal = $Terminal
    createdAt = (Get-Date).ToString("o")
    execute = [bool]$Execute
    status = "created"
} | ConvertTo-Json -Depth 10 | Set-Content -Path (Join-Path $runPath "run.json") -Encoding UTF8

$agents = ConvertTo-HashtableById -Items $committee.agents
$layers = Get-TaskLayers -Tasks $committee.tasks

$layerIndex = 0
foreach ($layer in $layers) {
    $layerIndex++
    Write-Host "Layer $layerIndex`: $(@($layer | ForEach-Object { $_.id }) -join ', ')"

    $jobs = @()
    foreach ($task in $layer) {
        $agent = $agents[$task.agentId]
        if ($null -eq $agent) {
            throw "Agente nao encontrado para task $($task.id): $($task.agentId)"
        }

        $prompt = New-AgentPrompt `
            -Committee $committee `
            -Agent $agent `
            -Task $task `
            -RunPath $runPath `
            -Ativo $Ativo `
            -ClasseResolvida $resolvedClass `
            -Objetivo $Objetivo `
            -ResolvedConfigPath $resolvedConfigPath `
            -Terminal $Terminal `
            -ExtraContext $ExtraContext

        $taskPromptPath = Join-Path $promptPath "$($task.id).prompt.md"
        $taskOutputPath = Join-Path $outputPath "$($task.id).md"
        $prompt | Set-Content -Path $taskPromptPath -Encoding UTF8

        if ($Execute) {
            if ([string]::IsNullOrWhiteSpace($AgentCommand)) {
                throw "Use -AgentCommand para executar. Sem -AgentCommand, rode sem -Execute para gerar prompts."
            }

            $jobs += Start-Job -ScriptBlock {
                param($CommandTemplate, $PromptPath, $OutputPath)
                $command = $CommandTemplate.Replace("{promptFile}", $PromptPath).Replace("{outputFile}", $OutputPath)
                powershell -NoProfile -ExecutionPolicy Bypass -Command $command
            } -ArgumentList $AgentCommand, $taskPromptPath, $taskOutputPath
        } elseif (-not (Test-Path $taskOutputPath)) {
            "Pendente. Execute o prompt correspondente em $taskPromptPath e salve a resposta aqui." |
                Set-Content -Path $taskOutputPath -Encoding UTF8
        }
    }

    if ($Execute -and $jobs.Count -gt 0) {
        Wait-Job -Job $jobs | Out-Null
        foreach ($job in $jobs) {
            Receive-Job -Job $job
            Remove-Job -Job $job
        }
    }
}

@{
    runId = "$($committee.name)-$Objetivo-$runStamp"
    committee = $committee.name
    ativo = $Ativo
    classe = $resolvedClass
    objetivo = $Objetivo
    configPath = $resolvedConfigPath
    terminal = $Terminal
    createdAt = (Get-Date).ToString("o")
    execute = [bool]$Execute
    status = "prompts_generated"
    runPath = $runPath
    finalMarkdown = (Join-Path $outputPath "decisao_final.md")
    finalJson = (Join-Path $outputPath "final.json")
} | ConvertTo-Json -Depth 10 | Set-Content -Path (Join-Path $runPath "run.json") -Encoding UTF8

Write-Host ""
Write-Host "Comite generico estruturado em:"
Write-Host $runPath
Write-Host ""
Write-Host "Classe resolvida:"
Write-Host $resolvedClass
Write-Host ""
Write-Host "Config alvo:"
Write-Host $resolvedConfigPath
Write-Host ""
Write-Host "Prompts:"
Write-Host $promptPath
Write-Host ""
Write-Host "Saidas:"
Write-Host $outputPath
