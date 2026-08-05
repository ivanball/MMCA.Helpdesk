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

$mmcaPinPattern = '<PackageVersion\s+Include="(MMCA\.Common[^"]*)"\s+Version="([^"]+)"'

function Get-MmcaPins {
    param([string] $PropsPath)

    if (-not (Test-Path $PropsPath)) { throw "No Directory.Packages.props at $PropsPath" }
    $pins = @(Select-String -Path $PropsPath -Pattern $mmcaPinPattern)
    if (-not $pins) { throw "No MMCA.Common.* PackageVersion entries in $PropsPath" }
    # Emitted one per pin so callers can pipe into Sort-Object / Where-Object. Every call site wraps
    # the result in @(): Set-StrictMode -Version Latest is on, and a one-element pipeline comes back
    # as a scalar whose .Count would then throw.
    $pins | ForEach-Object { $_.Matches[0].Groups[2].Value }
}

# What the seed itself pins. A generated app must land on exactly this by default: the template
# carries no version of its own, it inherits Directory.Packages.props from the tree it is staged from.
$seedVersions = @(Get-MmcaPins (Join-Path $repoRoot 'Directory.Packages.props') | Sort-Object -Unique)
if ($seedVersions.Count -ne 1) {
    throw "The seed's MMCA.Common.* pins are not in lockstep (ADR-016): $($seedVersions -join ', ')"
}
$seedVersion = $seedVersions[0]
Write-Host "Seed pins MMCA.Common.* at $seedVersion"

# --framework-version works by replacing that literal, so the token in template.json has to track the
# seed's pin. When it drifts it does not fail: it matches nothing, and the flag is accepted and
# silently ignored. Proving it here is cheap (no restore, no build) and it is the only check that
# would have caught the token sitting a hundred releases behind the tree it was replacing in.
Invoke-Step 'Explicit --framework-version is honored' {
    $probe = '9.9.9-smoke'
    $probeApp = 'Acme.Pin'

    Push-Location $WorkPath
    try {
        dotnet new mmca-app -n $probeApp --module Widgets --aggregate Widget --framework-version $probe --no-restore
        if ($LASTEXITCODE -ne 0) { throw "mmca-app --framework-version failed with exit code $LASTEXITCODE" }
    } finally {
        Pop-Location
    }

    $probeRoot = Join-Path $WorkPath $probeApp
    $stuck = @(Get-MmcaPins (Join-Path $probeRoot 'Directory.Packages.props') | Where-Object { $_ -ne $probe })
    if ($stuck) {
        throw "--framework-version $probe was accepted but $($stuck.Count) MMCA.Common.* pin(s) still read $($stuck[0]). The replace token in .template.config/template.json no longer matches the seed's version literal."
    }

    Write-Host "  every MMCA.Common.* pin moved to $probe"
    Remove-Item -Recurse -Force $probeRoot
}

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

    Invoke-Step "Framework pin $appName" {
        $pins = @(Get-MmcaPins (Join-Path $appRoot 'Directory.Packages.props'))
        $wrong = @($pins | Where-Object { $_ -ne $seedVersion } | Sort-Object -Unique)
        if ($wrong) {
            throw "Generated app pins MMCA.Common.* at $($wrong -join ', '), expected the seed's $seedVersion."
        }
        Write-Host "  $($pins.Count) MMCA.Common.* packages pinned at $seedVersion"
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
            #
            # No --child-collection, deliberately: this is the shape an adopter gets by default, and
            # it is the shape that used to ship a handler naming the seed's Comments collection.
            dotnet new mmca-command -n "Archive$($case.Aggregate)" --app $appName --module $case.Module --aggregate $case.Aggregate --domain-method Delete
            if ($LASTEXITCODE -ne 0) { throw "mmca-command failed with exit code $LASTEXITCODE" }

            # The other branch of the same conditional: an aggregate that does own a collection.
            dotnet new mmca-command -n "Restore$($case.Aggregate)" --app $appName --module $case.Module --aggregate $case.Aggregate --domain-method Delete --child-collection Comments
            if ($LASTEXITCODE -ne 0) { throw "mmca-command --child-collection failed with exit code $LASTEXITCODE" }

            # Same two branches on the read side. The query slice is staged from a GetById that maps
            # children into its DTO, so it carried the identical navigation leak.
            dotnet new mmca-query -n "Get$($case.Aggregate)ByNumber" --app $appName --module $case.Module --aggregate $case.Aggregate
            if ($LASTEXITCODE -ne 0) { throw "mmca-query failed with exit code $LASTEXITCODE" }

            dotnet new mmca-query -n "Find$($case.Aggregate)ByCode" --app $appName --module $case.Module --aggregate $case.Aggregate --child-collection Comments
            if ($LASTEXITCODE -ne 0) { throw "mmca-query --child-collection failed with exit code $LASTEXITCODE" }
        } finally {
            Pop-Location
        }
    }

    # Both branches of both slices, asserted on the generated text rather than only on the build.
    # Without --child-collection the eager-load must be EMPTY, not renamed: a build check alone would
    # pass on this app, whose aggregate does own Comments, and go on failing for every adopter whose
    # aggregate does not.
    Invoke-Step "Eager-load conditional $appName" {
        $bare = @(
            Join-Path $useCases "Archive$($case.Aggregate)/Archive$($case.Aggregate)Handler.cs"
            Join-Path $useCases "Get$($case.Aggregate)ByNumber/Get$($case.Aggregate)ByNumberHandler.cs"
        )
        $withChild = @(
            Join-Path $useCases "Restore$($case.Aggregate)/Restore$($case.Aggregate)Handler.cs"
            Join-Path $useCases "Find$($case.Aggregate)ByCode/Find$($case.Aggregate)ByCodeHandler.cs"
        )

        foreach ($handler in ($bare + $withChild)) {
            if (-not (Test-Path $handler)) { throw "No generated handler at $handler" }
            if ((Get-Content $handler -Raw) -match '//#if|//#else|//#endif') {
                throw "Conditional directives survived into $handler. The templating engine did not process them, so the file ships commented-out scaffolding."
            }
        }

        foreach ($handler in $bare) {
            $text = Get-Content $handler -Raw
            if ($text -match 'includes: \[nameof') {
                throw "Generated without --child-collection but $handler still names a navigation. It must fall back to an empty include list."
            }
            # Not merely absent: GetByIdAsync declares includes as a REQUIRED parameter, so omitting
            # the argument trades the missing-navigation error for CS7036.
            if ($text -notmatch 'includes: \[\],') {
                throw "Generated without --child-collection but $handler passes no includes argument at all. GetByIdAsync requires one."
            }
        }

        foreach ($handler in $withChild) {
            if ((Get-Content $handler -Raw) -notmatch 'includes: \[nameof\([A-Za-z]+\.Comments\)\]') {
                throw "Generated with --child-collection Comments but $handler has no matching eager-load."
            }
        }

        Write-Host "  empty include list without --child-collection, named navigation with it (command and query)"
    }

    Invoke-Step "Token sweep $appName slices" {
        $sliceFiles = @(Get-ChildItem -Path $useCases -Recurse -File |
            Where-Object { $_.DirectoryName -match 'Archive|Restore|ByNumber|ByCode' })
        if ($sliceFiles.Count -ne 8) { throw "Expected 8 slice files, found $($sliceFiles.Count)" }

        $offenders = $sliceFiles | ForEach-Object {
            $hit = Select-String -Path $_.FullName -Pattern 'helpdesk|ticket' -CaseSensitive:$false -List
            if ($hit) { "$($_.Name): $($hit.Line.Trim())" }
        }
        if ($offenders) {
            throw "Residual seed tokens in generated slices:`n  $($offenders -join "`n  ")"
        }
        Write-Host "  $($sliceFiles.Count) slice files, no residual tokens"
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

        # 6. database: its own Aspire database resource plus per-module DataSources routing. Each
        # module database carries its own dbo.OutboxMessages/InboxMessages tables, so two modules
        # migrated into one database collide on them.
        $shortLower = $short.ToLowerInvariant()
        $newModuleLower = $newModule.ToLowerInvariant()
        Edit-File "Source/Hosting/$appName.AppHost/Program.cs" `
            "var $($shortLower)Db = sql.AddDatabase(`"$shortLower`", `"$short`");" `
            "`nvar $($newModuleLower)Db = sql.AddDatabase(`"$shortLower-$newModuleLower`", `"${short}_$newModule`");"
        Edit-File "Source/Hosting/$appName.AppHost/Program.cs" `
            ".WithSQLServerDataSource($($shortLower)Db, `"$($case.Module)`")" `
            "`n    .WithSQLServerDataSource($($newModuleLower)Db, `"$newModule`")"

        # The top-level SQLServerMigrationsAssembly pin must GO: under Aspire the last
        # WithSQLServerDataSource call wins the top-level connection string, so one module always
        # collapses onto the Default source, and a top-level pin naming the other module's assembly
        # fails startup with "conflicting SQLServerMigrationsAssembly values". Mutated as JSON: the
        # instructions describe edits by meaning, not by byte offsets, so the smoke should too.
        $appSettingsPath = Join-Path $appRoot "Source/Hosts/$appName.Web/appsettings.json"
        $settings = Get-Content $appSettingsPath -Raw | ConvertFrom-Json

        if (-not $settings.ConnectionStrings.PSObject.Properties['SQLServerMigrationsAssembly']) {
            throw "Generated appsettings.json carries no top-level SQLServerMigrationsAssembly pin. The seed's appsettings shape moved; update smoke.ps1 and the mmca-module manualInstructions together."
        }
        $settings.ConnectionStrings.PSObject.Properties.Remove('SQLServerMigrationsAssembly')

        $settings.Modules | Add-Member -NotePropertyName $newModule -NotePropertyValue ([pscustomobject]@{ Enabled = $true })

        function New-DataSourceEntry {
            param([string] $Module)
            [pscustomobject]@{
                SQLServerConnectionString = "Server=localhost;Database=${short}_$Module;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
                SQLServerMigrationsAssembly = "$appName.Migrations.SqlServer.$Module"
            }
        }
        $settings | Add-Member -NotePropertyName 'DataSources' -NotePropertyValue ([pscustomobject]@{
            ($case.Module) = New-DataSourceEntry $case.Module
            ($newModule) = New-DataSourceEntry $newModule
        })
        $settings | ConvertTo-Json -Depth 10 | Set-Content -Path $appSettingsPath

        Write-Host "  all six wire-ups applied"
    }

    # The migrations project must arrive with Migrations/.editorconfig even though it ships no
    # migrations: `dotnet ef migrations add` never creates one, and without it the adopter's first
    # generated migration fails the build on analyzer errors inside code they did not write.
    Invoke-Step "Migrations .editorconfig shipped with $newModule" {
        $migrationsEditorConfig = Join-Path $appRoot "Source/Hosting/$appName.Migrations.SqlServer.$newModule/Migrations/.editorconfig"
        if (-not (Test-Path $migrationsEditorConfig)) {
            throw "mmca-module shipped no Migrations/.editorconfig at $migrationsEditorConfig."
        }
        if ((Get-Content $migrationsEditorConfig -Raw) -notmatch 'generated_code = true') {
            throw "The shipped Migrations/.editorconfig does not mark migration output as generated code."
        }
        Write-Host "  Migrations/.editorconfig present and marks output as generated code"
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
