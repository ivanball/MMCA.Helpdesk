<#
.SYNOPSIS
End-to-end smoke test for the MMCA.Templates pack: stage, pack, install, generate, build, test.

.DESCRIPTION
This is the correctness gate for the template, and it is NOT redundant with the repo's own CI.

The seed builds in LOCAL-SOURCE mode (its committed local.props points at ../MMCA.Common/Source),
while a generated app builds in PACKAGE mode against a released version from nuget.org. Those are
different builds: a source-mode build can pass where package-mode Release fails on an analyzer. A
green MMCA.Helpdesk CI run therefore says nothing about whether the template's output compiles.

It also greps the generated tree for residual seed tokens. sourceName and the symbol replacements
run as separate passes, so a token that only ever appears inside another one (Ticket inside Tickets,
Helpdesk inside MMCA.Helpdesk) is exactly where a rename silently half-applies.

.PARAMETER WorkPath
Scratch directory for the generated solutions. Defaults to a temp folder.

.PARAMETER SkipTests
Build only. The generated tests need no database, so there is rarely a reason to pass this.
#>
[CmdletBinding()]
param(
    [string] $WorkPath,
    [switch] $SkipTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$artifacts = Join-Path $repoRoot 'artifacts'

# Keep this SHORT. The generated tree nests deeply (Source/Modules/<Module>/<App>.<Module>.
# Application/obj/Release/net10.0/<App>.<Module>.Application.dll), and a long work path pushes the
# obj paths past MAX_PATH. The build then fails with MSB3030 "could not copy ... because it was not
# found", which reads like a template defect and is not one.
if (-not $WorkPath) {
    $WorkPath = Join-Path ([IO.Path]::GetTempPath()) 'mmca-tpl'
}
if ($WorkPath.Length -gt 60) {
    Write-Warning "WorkPath is $($WorkPath.Length) chars. Generated obj paths may exceed MAX_PATH and fail with MSB3030."
}
if (Test-Path $WorkPath) { Remove-Item -Recurse -Force $WorkPath }
New-Item -ItemType Directory -Path $WorkPath -Force | Out-Null

function Invoke-Step {
    param([string] $Name, [scriptblock] $Body)

    Write-Host ""
    Write-Host "=== $Name ===" -ForegroundColor Cyan
    & $Body
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE"
    }
}

Invoke-Step 'Stage' { & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'stage.ps1') -Clean }

Invoke-Step 'Pack' {
    Get-ChildItem -Path $artifacts -Filter '*.nupkg' -ErrorAction SilentlyContinue | Remove-Item -Force
    dotnet pack (Join-Path $PSScriptRoot 'MMCA.Templates.csproj') -o $artifacts
}

Invoke-Step 'Install' {
    # Installing the same package repeatedly leaves duplicate entries, and the next instantiation
    # dies on "Sequence contains more than one matching element". Clear it out first.
    while ((dotnet new uninstall 2>&1) -match 'MMCA\.Templates') {
        dotnet new uninstall MMCA.Templates | Out-Null
    }
    $nupkg = Get-ChildItem -Path $artifacts -Filter 'MMCA.Templates.*.nupkg' | Select-Object -First 1
    if (-not $nupkg) { throw "Pack produced no MMCA.Templates nupkg in $artifacts" }
    dotnet new install $nupkg.FullName
}

# Deliberately share no substring with the seed's own names: a rename that half-applies shows up as
# a leftover Helpdesk or Ticket, which the token sweep below then fails on.
$cases = @(
    @{ Name = 'Contoso.Support'; Module = 'Billing';      Aggregate = 'Invoice' },
    @{ Name = 'Zeta.Warehouse';  Module = 'Reservations'; Aggregate = 'Reservation' }
)

foreach ($case in $cases) {
    $appName = $case.Name

    # Generate straight into WorkPath: preferNameDirectory already creates the <AppName> folder, so
    # a per-case subdirectory would nest it twice and cost 20 more characters of path budget.
    Invoke-Step "Generate $appName (module $($case.Module), aggregate $($case.Aggregate))" {
        Push-Location $WorkPath
        try {
            dotnet new mmca-app -n $appName --module $case.Module --aggregate $case.Aggregate --no-restore
        } finally {
            Pop-Location
        }
    }

    $appRoot = Join-Path $WorkPath $appName
    $slnx = Join-Path $appRoot "$appName.slnx"
    if (-not (Test-Path $slnx)) { throw "Generated no $appName.slnx under $appRoot" }

    Invoke-Step "Token sweep $appName" {
        $offenders = Get-ChildItem -Path $appRoot -Recurse -File -Force |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
            ForEach-Object {
                $hit = Select-String -Path $_.FullName -Pattern 'helpdesk|ticket' -SimpleMatch:$false -CaseSensitive:$false -List
                if ($hit) { "$($_.FullName.Substring($appRoot.Length)): $($hit.Line.Trim())" }
            }

        if ($offenders) {
            throw @"
Residual seed tokens survived the rename in $($offenders.Count) file(s):
  $($offenders -join "`n  ")
"@
        }
        Write-Host "  no residual 'helpdesk' or 'ticket' tokens"
    }

    Invoke-Step "Build $appName (package mode)" {
        dotnet build $slnx -c Release
    }

    if (-not $SkipTests) {
        Invoke-Step "Test $appName" {
            dotnet test --solution $slnx -c Release --no-build --minimum-expected-tests 1
        }
    }

    # Slices only need proving once; running them for every app case would double the smoke's
    # wall-clock for no extra coverage.
    if ($appName -ne $cases[0].Name) { continue }

    $useCases = Join-Path $appRoot "Source/Modules/$($case.Module)/$appName.$($case.Module).Application/$($case.Module)/UseCases"
    if (-not (Test-Path $useCases)) { throw "No UseCases folder at $useCases" }

    Invoke-Step "Generate slices into $appName" {
        Push-Location $useCases
        try {
            # --domain-method Delete: the command slice calls a guarded method on the aggregate, and
            # the scaffold cannot invent one. Pointing it at a method the generated aggregate
            # already has is what makes this step a compile check rather than a rename check.
            dotnet new mmca-command -n "Archive$($case.Aggregate)" --app $appName --module $case.Module --aggregate $case.Aggregate --domain-method Delete
            if ($LASTEXITCODE -ne 0) { throw "mmca-command failed with exit code $LASTEXITCODE" }

            dotnet new mmca-query -n "Get$($case.Aggregate)ByNumber" --app $appName --module $case.Module --aggregate $case.Aggregate
            if ($LASTEXITCODE -ne 0) { throw "mmca-query failed with exit code $LASTEXITCODE" }
        } finally {
            Pop-Location
        }
    }

    Invoke-Step "Token sweep $appName slices" {
        $sliceFiles = Get-ChildItem -Path $useCases -Recurse -File |
            Where-Object { $_.DirectoryName -match 'Archive|ByNumber' }
        if ($sliceFiles.Count -ne 4) { throw "Expected 4 slice files, found $($sliceFiles.Count)" }

        $offenders = $sliceFiles | ForEach-Object {
            $hit = Select-String -Path $_.FullName -Pattern 'helpdesk|ticket' -CaseSensitive:$false -List
            if ($hit) { "$($_.Name): $($hit.Line.Trim())" }
        }
        if ($offenders) {
            throw "Residual seed tokens in generated slices:`n  $($offenders -join "`n  ")"
        }
        Write-Host "  4 slice files, no residual tokens"
    }

    Invoke-Step "Rebuild $appName with slices" { dotnet build $slnx -c Release }

    # ---- mmca-module, including the wire-ups it can only print ---------------------------------
    # Applying them here is the point: the manualInstructions are the template's most fragile
    # deliverable, and a step missing from that list (project references were, at first) shows up
    # as a compile error nowhere else.
    $newModule = 'Shipping'
    $newAggregate = 'Shipment'
    $short = $appName.Split('.')[-1]

    Invoke-Step "Generate module $newModule into $appName" {
        Push-Location $appRoot
        try {
            dotnet new mmca-module -n $newModule --app $appName --aggregate $newAggregate
            if ($LASTEXITCODE -ne 0) { throw "mmca-module failed with exit code $LASTEXITCODE" }
        } finally {
            Pop-Location
        }
    }

    Invoke-Step "Wire up module $newModule" {
        # 1. solution
        Push-Location $appRoot
        try {
            $projects = @(
                Get-ChildItem "Source/Modules/$newModule" -Recurse -Filter '*.csproj'
                Get-ChildItem "Tests/Modules/$newModule" -Recurse -Filter '*.csproj'
                Get-ChildItem "Source/Hosting/$appName.Migrations.SqlServer.$newModule" -Filter '*.csproj'
            ) | ForEach-Object { $_.FullName }
            if ($projects.Count -ne 8) { throw "Expected 8 generated projects, found $($projects.Count)" }
            dotnet sln $slnx add @projects | Out-Null
        } finally {
            Pop-Location
        }

        function Edit-File {
            param([string] $Path, [string] $Anchor, [string] $Addition)

            $full = Join-Path $appRoot $Path
            $text = Get-Content $full -Raw
            if ($text -notmatch [regex]::Escape($Anchor)) {
                throw "Wire-up anchor not found in ${Path}: $Anchor"
            }
            Set-Content -Path $full -Value $text.Replace($Anchor, $Anchor + $Addition) -NoNewline
        }

        # 2. project references: the host and the architecture tests
        Edit-File "Source/Hosts/$appName.Web/$appName.Web.csproj" `
            "<ProjectReference Include=`"..\..\Modules\$($case.Module)\$appName.$($case.Module).API\$appName.$($case.Module).API.csproj`" />" `
            "`n    <ProjectReference Include=`"..\..\Hosting\$appName.Migrations.SqlServer.$newModule\$appName.Migrations.SqlServer.$newModule.csproj`" />`n    <ProjectReference Include=`"..\..\Modules\$newModule\$appName.$newModule.API\$appName.$newModule.API.csproj`" />"

        $layerRefs = @('Domain', 'Application', 'Infrastructure', 'Shared', 'API') | ForEach-Object {
            "`n    <ProjectReference Include=`"..\..\..\Source\Modules\$newModule\$appName.$newModule.$_\$appName.$newModule.$_.csproj`" />"
        }
        Edit-File "Tests/Architecture/$appName.Architecture.Tests/$appName.Architecture.Tests.csproj" `
            "<ProjectReference Include=`"..\..\..\Source\Modules\$($case.Module)\$appName.$($case.Module).API\$appName.$($case.Module).API.csproj`" />" `
            ($layerRefs -join '')

        # 3. identifier alias
        Edit-File 'Directory.Build.props' `
            "Condition=`"'`$(MSBuildProjectName)' != '$appName.$($case.Module).Shared'`" />" `
            "`n    <Compile Include=`"`$(MSBuildThisFileDirectory)Source\Modules\$newModule\$appName.$newModule.Shared\$appName.$newModule.GlobalUsings.IdentifierType.cs`"`n             Link=`"GlobalUsings\$appName.$newModule.GlobalUsings.IdentifierType.cs`"`n             Condition=`"'`$(MSBuildProjectName)' != '$appName.$newModule.Shared'`" />"

        # 4. architecture map
        $mapLines = @(
            "`n`n        // $newModule module"
            "`n        Module(`"$newModule`", Layer.Domain, typeof($appName.$newModule.Domain.$newModule.$newAggregate).Assembly),"
            "`n        Module(`"$newModule`", Layer.Application, typeof($appName.$newModule.Application.ClassReference).Assembly),"
            "`n        Module(`"$newModule`", Layer.Infrastructure, typeof($appName.$newModule.Infrastructure.AssemblyReference).Assembly),"
            "`n        Module(`"$newModule`", Layer.Shared, typeof($appName.$newModule.Shared.$newModule.${newAggregate}DTO).Assembly),"
            "`n        Module(`"$newModule`", Layer.Api, typeof($appName.$newModule.API.Controllers.${newModule}Controller).Assembly),"
        ) -join ''
        Edit-File "Tests/Architecture/$appName.Architecture.Tests/${short}ArchitectureMap.cs" `
            "Module(`"$($case.Module)`", Layer.Api, typeof($appName.$($case.Module).API.Controllers.$($case.Module)Controller).Assembly)," `
            $mapLines

        # 5. host error resources
        Edit-File "Source/Hosts/$appName.Web/Program.cs" `
            "using $appName.$($case.Module).API.Resources;" `
            "`nusing $appName.$newModule.API.Resources;"
        Edit-File "Source/Hosts/$appName.Web/Program.cs" `
            "services.AddErrorResources<$($case.Module)ErrorResources>();" `
            "`nservices.AddErrorResources<${newModule}ErrorResources>();"

        Write-Host "  all five wire-ups applied"
    }

    Invoke-Step "Rebuild $appName with two modules" { dotnet build $slnx -c Release }

    if (-not $SkipTests) {
        Invoke-Step "Test $appName with two modules" {
            dotnet test --solution $slnx -c Release --no-build --minimum-expected-tests 1
        }
    }
}

Write-Host ""
Write-Host "Template smoke passed for: $($cases.Name -join ', ')" -ForegroundColor Green
