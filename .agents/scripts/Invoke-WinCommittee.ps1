param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("pre-abertura", "meio-dia", "pos-pregao")]
    [string]$Comite,

    [string]$Symbol = "WINQ26",

    [string]$RootPath = "D:\SistemEarobot\financial.robot",

    [switch]$Execute,

    [string]$AgentCommand = "",

    [string]$ExtraContext = ""
)

$ErrorActionPreference = "Stop"

function Get-CommitteeFileName {
    param([string]$Comite)

    switch ($Comite) {
        "pre-abertura" { return "win-pre-abertura.json" }
        "meio-dia" { return "win-meio-dia.json" }
        "pos-pregao" { return "win-pos-pregao.json" }
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

function New-AgentPrompt {
    param(
        [object]$Committee,
        [object]$Agent,
        [object]$Task,
        [string]$RunPath,
        [string]$Symbol,
        [string]$ExtraContext
    )

    $skillList = if ($Agent.skills.Count -gt 0) { ($Agent.skills -join ", ") } else { "nenhuma skill especifica" }
    $dependencyText = if ($Task.dependsOn.Count -gt 0) {
        ($Task.dependsOn | ForEach-Object { "- $_`: $RunPath\outputs\$_.md" }) -join [Environment]::NewLine
    } else {
        "Sem dependencias."
    }

    return @"
Voce e um agente local do comite do robo financial.robot.

Comite: $($Committee.name) - $($Committee.title)
Ativo: $Symbol
Papel: $($Agent.role)
TaskId: $($Task.id)
Skills a usar: $skillList

Leia as skills necessarias em:
$RootPath\.agents\skills

Contexto principal do projeto:
- Catalogo de estrategias: $RootPath\docs\catalogo-estrategias.md
- Configs locais: $RootPath\config
- Dados/extracoes: $RootPath\data e $RootPath\publish-extractor
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
3. Separe fato, calculo, inferencia e cenario.
4. Nao promova config ativo nem altere D:\SistemEarobot\config.
5. Gere uma saida objetiva em Markdown e inclua um bloco JSON ao final quando aplicavel.
6. Se dados forem insuficientes, diga claramente o que faltou e use bloqueio conservador.

Entregavel:
Grave ou retorne o parecer da task $($Task.id) para:
$RunPath\outputs\$($Task.id).md
"@
}

function Invoke-AgentCommand {
    param(
        [string]$CommandTemplate,
        [string]$PromptPath,
        [string]$OutputPath
    )

    if ([string]::IsNullOrWhiteSpace($CommandTemplate)) {
        throw "Use -AgentCommand para executar. Sem -AgentCommand, rode sem -Execute para gerar prompts."
    }

    $command = $CommandTemplate.Replace("{promptFile}", $PromptPath).Replace("{outputFile}", $OutputPath)
    powershell -NoProfile -ExecutionPolicy Bypass -Command $command
}

$committeeFile = Join-Path $RootPath (Join-Path ".agents\committees" (Get-CommitteeFileName -Comite $Comite))
if (-not (Test-Path $committeeFile)) {
    throw "Definicao de comite nao encontrada: $committeeFile"
}

$committee = Get-Content $committeeFile -Raw | ConvertFrom-Json
$runStamp = Get-Date -Format "yyyyMMdd-HHmmss"
$dateFolder = Get-Date -Format "yyyy-MM-dd"
$runPath = Join-Path $RootPath ".agents\runs\committees\$Symbol\$dateFolder\$($committee.name)-$runStamp"
$promptPath = Join-Path $runPath "prompts"
$outputPath = Join-Path $runPath "outputs"

New-Item -ItemType Directory -Force -Path $promptPath, $outputPath | Out-Null

$committee | ConvertTo-Json -Depth 20 | Set-Content -Path (Join-Path $runPath "committee.json") -Encoding UTF8
@{
    runId = "$($committee.name)-$runStamp"
    committee = $committee.name
    symbol = $Symbol
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

        $prompt = New-AgentPrompt -Committee $committee -Agent $agent -Task $task -RunPath $runPath -Symbol $Symbol -ExtraContext $ExtraContext
        $taskPromptPath = Join-Path $promptPath "$($task.id).prompt.md"
        $taskOutputPath = Join-Path $outputPath "$($task.id).md"
        $prompt | Set-Content -Path $taskPromptPath -Encoding UTF8

        if ($Execute) {
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
    runId = "$($committee.name)-$runStamp"
    committee = $committee.name
    symbol = $Symbol
    createdAt = (Get-Date).ToString("o")
    execute = [bool]$Execute
    status = "prompts_generated"
    runPath = $runPath
    finalMarkdown = (Join-Path $outputPath "decisao_final.md")
    finalJson = (Join-Path $outputPath "final.json")
} | ConvertTo-Json -Depth 10 | Set-Content -Path (Join-Path $runPath "run.json") -Encoding UTF8

Write-Host ""
Write-Host "Comite estruturado em:"
Write-Host $runPath
Write-Host ""
Write-Host "Prompts:"
Write-Host $promptPath
Write-Host ""
Write-Host "Saidas:"
Write-Host $outputPath
