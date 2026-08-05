<#
.SYNOPSIS
Stages the MMCA.Helpdesk tree as dotnet-new template content.

.DESCRIPTION
The reference seed IS the template: there is no second copy of the solution anywhere, so the
template cannot drift from the app whose CI proves it builds. This script is the only mechanical
step. It copies the repo tree into artifacts/template-staging/, drops the files that belong to the
seed's own repo rather than to a generated app, and lays the overlay/ files on top.

Everything else (renaming MMCA.Helpdesk -> the adopter's name, Tickets -> their module, Ticket ->
their aggregate) is done by dotnet new at instantiation time from .template.config/template.json.

Run it before packing, or to refresh a local install:

    pwsh build/templates/stage.ps1
    dotnet pack build/templates/MMCA.Templates.csproj -o ./artifacts
    dotnet new install ./artifacts/MMCA.Templates.*.nupkg --force

.PARAMETER OutputPath
Staging root. Defaults to artifacts/template-staging/ (gitignored).

.PARAMETER Clean
Delete the staging root before copying.
#>
[CmdletBinding()]
param(
    [string] $OutputPath,
    [switch] $Clean
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$overlayRoot = Join-Path $PSScriptRoot 'overlay'

if (-not $OutputPath) {
    $OutputPath = Join-Path $repoRoot 'artifacts/template-staging'
}

if ($Clean -and (Test-Path $OutputPath)) {
    Remove-Item -Recurse -Force $OutputPath
}

# Directories never copied into any template. Matched against path segments.
$excludedDirs = @(
    '.git', '.vs', '.idea', '.vscode',
    'bin', 'obj', 'out', 'artifacts', 'nupkgs', 'logs',
    'build', 'templates'
)

# Files that belong to THIS repo rather than to a generated app. The overlay supplies replacements
# for README.md, .gitignore, local.props, and the CI workflow.
$excludedFiles = @(
    'local.props',       # the seed's ACTIVE local-source override; overlay ships an opt-in one
    'README.md',         # describes the reference app, not the adopter's app
    'CLAUDE.md',         # agent guidance for this repo
    'CONTRIBUTING.md',   # this repo's PR rules
    'LICENSE',           # the seed's license is not the adopter's license
    '.gitignore'         # seed-specific: it deliberately does NOT ignore local.props
)

function Test-Excluded {
    param([string] $RelativePath)

    $segments = $RelativePath -split '[\\/]'
    foreach ($segment in $segments[0..($segments.Length - 2)]) {
        if ($excludedDirs -contains $segment) { return $true }
    }

    $leaf = $segments[-1]
    if ($excludedFiles -contains $leaf) { return $true }
    if ($leaf -like '*.user' -or $leaf -like '*.suo') { return $true }

    # .github/ here is THIS repo's governance (CI against MMCA.Common source, CODEOWNERS, the PR
    # template, the Claude review workflows), not app content. The overlay ships a generated-app CI
    # workflow in its place.
    if (($RelativePath -replace '\\', '/') -like '.github/*') { return $true }

    return $false
}

function Copy-Tree {
    param(
        [string] $Source,
        [string] $Destination,
        [string[]] $Include
    )

    $roots = if ($Include) { $Include | ForEach-Object { Join-Path $Source $_ } } else { @($Source) }

    foreach ($root in $roots) {
        if (-not (Test-Path $root)) { continue }

        $items = if ((Get-Item $root).PSIsContainer) {
            Get-ChildItem -Path $root -Recurse -File -Force
        } else {
            @(Get-Item $root)
        }

        foreach ($item in $items) {
            $relative = $item.FullName.Substring($Source.Length).TrimStart('\', '/')
            if (Test-Excluded $relative) { continue }

            $target = Join-Path $Destination $relative
            $targetDir = Split-Path $target -Parent
            if (-not (Test-Path $targetDir)) {
                New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
            }
            Copy-Item -Path $item.FullName -Destination $target -Force
        }
    }
}

# ---- mmca-app: the whole solution -------------------------------------------------------------
$appStaging = Join-Path $OutputPath 'mmca-app'
if (Test-Path $appStaging) { Remove-Item -Recurse -Force $appStaging }
New-Item -ItemType Directory -Path $appStaging -Force | Out-Null

Copy-Tree -Source $repoRoot -Destination $appStaging

# The overlay ships as 'gitignore', renamed to '.gitignore' on the way in. Kept dotless in the repo
# because a real .gitignore sitting in the overlay directory ignores its own siblings: it lists
# local.props, so the file that --local-mmca is supposed to emit would never be committed, and the
# template would silently produce nothing for that flag from a fresh clone.
$overlayRenames = @{ 'gitignore' = '.gitignore' }

$appOverlay = Join-Path $overlayRoot 'mmca-app'
if (-not (Test-Path $appOverlay)) { throw "No overlay at $appOverlay" }

Get-ChildItem -Path $appOverlay -Recurse -File -Force | ForEach-Object {
    $relative = $_.FullName.Substring($appOverlay.Length).TrimStart('\', '/')
    foreach ($from in $overlayRenames.Keys) {
        if ([IO.Path]::GetFileName($relative) -eq $from) {
            # Split-Path returns '' for a file at the overlay root, and Join-Path rejects that.
            $parent = Split-Path $relative -Parent
            $relative = if ($parent) { Join-Path $parent $overlayRenames[$from] } else { $overlayRenames[$from] }
        }
    }

    $target = Join-Path $appStaging $relative
    $targetDir = Split-Path $target -Parent
    if (-not (Test-Path $targetDir)) {
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
    }
    Copy-Item -Path $_.FullName -Destination $target -Force
}

# Every overlay file must land, or a generated app quietly loses a capability rather than failing.
foreach ($expected in @('README.md', '.gitignore', 'local.props', '.github/workflows/ci.yml')) {
    if (-not (Test-Path (Join-Path $appStaging $expected))) {
        throw "Overlay file '$expected' did not reach staging. Check build/templates/overlay/mmca-app/ and whether a .gitignore is hiding it from the repo."
    }
}

# ---- derived: pin the template's framework version to the seed's own ----------------------------
# The template does not carry its own MMCA.Common version. Directory.Packages.props is copied from
# the seed, so what a generated app pins is whatever the seed pins, and --framework-version works by
# replacing that literal. That makes the version in template.json a DERIVED value, and one that had
# silently died: it still read 1.135.0 after the seed moved on, so the replace token matched nothing,
# --framework-version was accepted and ignored, and every generated app got the seed's pin whatever
# the adopter asked for. Deriving it here is what keeps the flag alive across future bumps: a
# `Bump MMCA.Common to vX.Y.Z` PR touches only Directory.Packages.props, as it should.
$packagesProps = Join-Path $appStaging 'Directory.Packages.props'
if (-not (Test-Path $packagesProps)) {
    throw "No Directory.Packages.props in staging. Generated apps would have no package versions at all."
}

$propsText = Get-Content $packagesProps -Raw
$pinPattern = '<PackageVersion\s+Include="([^"]+)"\s+Version="([^"]+)"\s*/>'
$allPins = [regex]::Matches($propsText, $pinPattern)

$mmcaPins = @($allPins | Where-Object { $_.Groups[1].Value -like 'MMCA.Common*' })
if (-not $mmcaPins) {
    throw "No MMCA.Common.* PackageVersion entries in $packagesProps. The framework pins moved; update stage.ps1."
}

# ADR-016: every MMCA.Common.* package moves together, no phased rollout and no skew across the set.
# A split here would make "the version" ambiguous and ship a template that pins two of them.
# @() throughout: Set-StrictMode -Version Latest is on, and a one-element pipeline is a scalar.
$distinctVersions = @($mmcaPins | ForEach-Object { $_.Groups[2].Value } | Sort-Object -Unique)
if ($distinctVersions.Count -ne 1) {
    throw "The seed's MMCA.Common.* pins are not in lockstep (ADR-016): $($distinctVersions -join ', '). Bring them to one version before staging."
}

$frameworkVersion = $distinctVersions[0]

# --framework-version substitutes a bare version literal, so any OTHER package sitting on that same
# version would be rewritten with it. Harmless today and silently wrong the day it is not.
$collisions = @($allPins |
    Where-Object { $_.Groups[1].Value -notlike 'MMCA.Common*' -and $_.Groups[2].Value -eq $frameworkVersion } |
    ForEach-Object { $_.Groups[1].Value })

if ($collisions) {
    throw @"
Non-MMCA packages also pin $frameworkVersion, and --framework-version replaces that literal wherever
it appears, so generating with a different version would silently retarget them too:
  $($collisions -join "`n  ")
Make the replace token in .template.config/template.json more specific before staging.
"@
}

$templateJsonPath = Join-Path $appStaging '.template.config/template.json'
$hostJsonPath = Join-Path $appStaging '.template.config/dotnetcli.host.json'
$templateJson = Get-Content $templateJsonPath -Raw

$declared = [regex]::Match($templateJson, '(?s)"frameworkVersion"\s*:\s*\{.*?"defaultValue"\s*:\s*"([^"]+)"')
if (-not $declared.Success) {
    throw "No frameworkVersion.defaultValue in $templateJsonPath. The symbol was renamed or removed; update stage.ps1."
}

$declaredVersion = $declared.Groups[1].Value
if ($declaredVersion -ne $frameworkVersion) {
    foreach ($config in @($templateJsonPath, $hostJsonPath)) {
        $text = Get-Content $config -Raw
        Set-Content -Path $config -Value $text.Replace($declaredVersion, $frameworkVersion) -NoNewline
    }
    Write-Host "frameworkVersion re-stamped: $declaredVersion -> $frameworkVersion (from Directory.Packages.props)"
} else {
    Write-Host "frameworkVersion: $frameworkVersion (matches Directory.Packages.props)"
}

# ---- derived .editorconfig delta ---------------------------------------------------------------
# Scaffolding renames every namespace, which moves where the app's OWN usings sort relative to
# MMCA.Common.* and third-party ones. "using Contoso.Support.Billing.Shared;" belongs above
# "using MMCA.Common...;" but "using Zeta.App...;" belongs below it, so no single checked-in order
# is correct for every generated name and SA1210 fails the first build. (SA1210 has no notion of
# blank-line-separated groups, so spacing them apart does not help.)
#
# This is appended to the STAGED copy, never to the seed's own .editorconfig: that file stays the
# single source of the shared analyzer baseline that compare-analyzer-config.ps1 enforces across the
# four repos. One rule is relaxed, the other 213 severities are untouched, and the generated app
# carries the one command that restores full strictness.
$editorConfig = Join-Path $appStaging '.editorconfig'
if (-not (Test-Path $editorConfig)) {
    throw "No .editorconfig in staging. Generated apps would build without the five analyzers."
}

$delta = @'

# ---------------------------------------------------------------------------------------------
# SCAFFOLD DELTA (added by dotnet new mmca-app)
#
# Scaffolding renamed every namespace in this solution. That changes where your own usings sort
# against MMCA.Common.* and the third-party ones, so the using directives you were handed are not
# in alphabetical order and SA1210 would fail your first build.
#
# Sort them with:
#
#     dotnet format analyzers <YourApp>.slnx --diagnostics SA1210 --severity error
#
# Every dotnet new mmca-command / mmca-query slice arrives with the same skew, so either re-run
# that command after scaffolding, or leave this block in place until you have stopped scaffolding
# and then delete it to get the full baseline back.
#
# Only this one rule is relaxed. Every other analyzer stays at error severity.
# ---------------------------------------------------------------------------------------------
[*.cs]
dotnet_diagnostic.SA1210.severity = suggestion
'@

# Idempotent: staging may be re-run over an existing tree.
if ((Get-Content $editorConfig -Raw) -notmatch 'SCAFFOLD DELTA') {
    Add-Content -Path $editorConfig -Value $delta -NoNewline
}

# ---- derived: drop the inherited wire-contract freeze -------------------------------------------
# IntegrationEventContractTestsBase compares a checked-in literal against the actual events with
# their members sorted alphabetically. "{ RequesterUserId, TicketId }" is correct for Ticket and
# wrong for Invoice, because the aggregate's own Id property moves position: no single literal is
# right for every name the scaffold can be given.
#
# Removing the subclass is also the correct answer on its own terms. A frozen wire contract
# inherited from someone else's sample module guarantees nothing. The adopter freezes theirs, once,
# against their own events; the generated README carries the class to paste and the command that
# prints the value. The seed keeps its own frozen contract, green and unchanged.
#
# Deleted rather than commented out: S125 (commented-out code) is a warning, and
# TreatWarningsAsErrors makes that a build error.
$archTests = Join-Path $appStaging 'Tests/Architecture/MMCA.Helpdesk.Architecture.Tests/ArchitectureTests.cs'
if (-not (Test-Path $archTests)) {
    throw "Expected $archTests in staging. The architecture-fitness map moved; update stage.ps1."
}

$archSource = Get-Content $archTests -Raw
$contractClass = '(?ms)public sealed class IntegrationEventContractTests\s*:\s*IntegrationEventContractTestsBase\r?\n\{.*?\r?\n\}\r?\n\r?\n'
$matchCount = ([regex]::Matches($archSource, $contractClass)).Count

if ($matchCount -ne 1) {
    throw "Expected exactly one IntegrationEventContractTests class in ArchitectureTests.cs, found $matchCount. Update the pattern in stage.ps1 rather than shipping a template whose first test run is red."
}

Set-Content -Path $archTests -Value ([regex]::Replace($archSource, $contractClass, '')) -NoNewline

$appFileCount = (Get-ChildItem -Path $appStaging -Recurse -File -Force).Count
Write-Host "mmca-app staged: $appFileCount files -> $appStaging"

if (-not (Test-Path (Join-Path $appStaging '.template.config/template.json'))) {
    throw "Staging produced no .template.config/template.json. The template would pack empty."
}

# ---- slice templates: staged from the seed's own use-case folders -------------------------------
# Same rule as mmca-app: the seed's files ARE the template. Only the .template.config lives under
# templates/; the .cs comes straight from the module the seed keeps compiling and testing.
$sliceRoot = Join-Path $repoRoot 'templates'
$useCases = Join-Path $repoRoot 'Source/Modules/Tickets/MMCA.Helpdesk.Tickets.Application/Tickets/UseCases'

$slices = @(
    @{ Name = 'mmca-command'; Source = 'Delete';  Files = @('DeleteTicketCommand.cs', 'DeleteTicketHandler.cs') },
    @{ Name = 'mmca-query';   Source = 'GetById'; Files = @('GetTicketByIdQuery.cs', 'GetTicketByIdHandler.cs') }
)

foreach ($slice in $slices) {
    $target = Join-Path $OutputPath $slice.Name
    if (Test-Path $target) { Remove-Item -Recurse -Force $target }
    New-Item -ItemType Directory -Path $target -Force | Out-Null

    $configSource = Join-Path $sliceRoot "$($slice.Name)/.template.config"
    if (-not (Test-Path $configSource)) {
        throw "No .template.config for $($slice.Name) at $configSource"
    }
    Copy-Item -Path $configSource -Destination (Join-Path $target '.template.config') -Recurse -Force

    foreach ($file in $slice.Files) {
        $from = Join-Path $useCases "$($slice.Source)/$file"
        if (-not (Test-Path $from)) {
            throw "$($slice.Name) sources from $from, which no longer exists. The seed's use-case layout moved; update stage.ps1."
        }
        Copy-Item -Path $from -Destination (Join-Path $target $file) -Force
    }

    Write-Host "$($slice.Name) staged: $($slice.Files.Count) files -> $target"
}

# ---- derived: make each slice's eager-load optional ----------------------------------------------
# Both slices are staged from use cases whose aggregate owns a Comments collection: the command from
# Delete (loaded tracked so the soft-delete cascades), the query from GetById (loaded to map children
# into the DTO). --aggregate renames the type but nothing renames the navigation, so an adopter whose
# aggregate has no Comments was handed a slice that could not compile: 'Order' does not contain a
# definition for 'Comments'. A dotnet-new conditional lets --child-collection name their own
# navigation instead.
#
# The false branch passes an EMPTY list rather than omitting the argument. IEntityReader.GetByIdAsync
# declares includes as a required parameter, so dropping the line trades one compile error for
# another (CS7036).
#
# Injected into the STAGED copies only, never into the seed's own handlers. Those are real code in a
# module whose tests run, and //#if lines in them would also reach mmca-app and mmca-module, where
# the symbol is not defined: the condition would evaluate false and the eager-load would silently
# degrade to an empty list in generated apps whose aggregate DOES own the collection.
foreach ($slice in $slices) {
    $handler = Join-Path $OutputPath "$($slice.Name)/$($slice.Files | Where-Object { $_ -like '*Handler.cs' })"
    if (-not (Test-Path $handler)) {
        throw "Expected a handler at $handler. The $($slice.Name) slice file list moved; update stage.ps1."
    }

    $handlerSource = Get-Content $handler -Raw
    $includeLine = '(?m)^([ \t]*)(includes: \[nameof\(Ticket\.Comments\)\],)(\r?\n)'
    $includeMatches = ([regex]::Matches($handlerSource, $includeLine)).Count

    if ($includeMatches -ne 1) {
        throw "Expected exactly one 'includes: [nameof(Ticket.Comments)]' line in the staged $($slice.Name) handler, found $includeMatches. The seed's $($slice.Source) handler changed shape; update the pattern in stage.ps1 rather than shipping a slice that names a collection the adopter's aggregate may not have."
    }

    # Directives at column 0, reusing the line's own terminator so the staged file does not acquire
    # mixed line endings. The engine strips whole directive lines in both branches, so the emitted C#
    # keeps the argument's own indentation and no comment survives into generated code.
    $conditional = '//#if (hasChildCollection)$3$1$2$3//#else$3$1includes: [],$3//#endif$3'
    Set-Content -Path $handler -Value ([regex]::Replace($handlerSource, $includeLine, $conditional)) -NoNewline

    Write-Host "$($slice.Name): eager-load made conditional on --child-collection"
}

# ---- mmca-module: the five layer projects, both test projects, the migrations project ----------
# Staged with the repo-relative folder structure intact, so instantiating from an app root drops
# each project exactly where the solution expects it.
$moduleStaging = Join-Path $OutputPath 'mmca-module'
if (Test-Path $moduleStaging) { Remove-Item -Recurse -Force $moduleStaging }
New-Item -ItemType Directory -Path $moduleStaging -Force | Out-Null

Copy-Tree -Source $repoRoot -Destination $moduleStaging -Include @(
    'Source/Modules/Tickets',
    'Tests/Modules/Tickets',
    'Source/Hosting/MMCA.Helpdesk.Migrations.SqlServer.Tickets'
)

# The seed's migrations describe the Ticket schema. A new module starts with none: the adopter runs
# `dotnet ef migrations add InitialCreate` against their own entities. Shipping the seed's would
# produce a first migration for tables the adopter never declared. The folder's .editorconfig is the
# one file that DOES ship: it marks migration output as generated code so the five analyzers skip
# it, and `dotnet ef` never recreates it, so dropping it would fail the adopter's first build after
# `migrations add` with style errors inside code they did not write.
$stagedMigrations = Join-Path $moduleStaging 'Source/Hosting/MMCA.Helpdesk.Migrations.SqlServer.Tickets/Migrations'
if (-not (Test-Path (Join-Path $stagedMigrations '.editorconfig'))) {
    throw "No .editorconfig under the seed's Migrations folder reached staging. Generated migrations would build under the full analyzer baseline; restore it before staging."
}
Get-ChildItem -Path $stagedMigrations -File -Force |
    Where-Object { $_.Name -ne '.editorconfig' } |
    Remove-Item -Force

$moduleConfig = Join-Path $sliceRoot 'mmca-module/.template.config'
if (-not (Test-Path $moduleConfig)) { throw "No .template.config for mmca-module at $moduleConfig" }
Copy-Item -Path $moduleConfig -Destination (Join-Path $moduleStaging '.template.config') -Recurse -Force

$moduleFileCount = (Get-ChildItem -Path $moduleStaging -Recurse -File -Force).Count
Write-Host "mmca-module staged: $moduleFileCount files -> $moduleStaging"

# The seed's own local.props must never reach an adopter. It is ACTIVE (UseLocalMMCA=true), so a
# generated app carrying it would fail to build with a confusing MSBuild error the moment
# ../MMCA.Common/Source is not on disk. Only the overlay's opt-in copy may ship.
$stagedLocalProps = Join-Path $appStaging 'local.props'
if ((Test-Path $stagedLocalProps) -and
    ((Get-Content $stagedLocalProps -Raw) -match 'COMMITTED in this reference seed')) {
    throw "The seed's active local.props leaked into staging. Only the overlay's opt-in copy may ship."
}

# NuGet treats a PackagePath with no file extension as a DIRECTORY and appends the item's relative
# path to it, so an extensionless file packs to content/x/y/FILE/x/y/FILE. It is not an error and
# not a warning: the package builds clean and the generated tree is quietly wrong. Fail here
# instead, where the message can say what to do.
$extensionless = Get-ChildItem -Path $appStaging -Recurse -File -Force |
    Where-Object { -not $_.Extension } |
    ForEach-Object { $_.FullName.Substring($appStaging.Length).TrimStart('\', '/') }

if ($extensionless) {
    throw @"
Extensionless files cannot be packed into a template: NuGet would double their path.
Either give them an extension or exclude them in `$excludedFiles / Test-Excluded:
  $($extensionless -join "`n  ")
"@
}

Write-Host "Staging complete."
