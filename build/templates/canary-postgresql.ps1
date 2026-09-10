<#
.SYNOPSIS
PostgreSQL canary: generate the template with --database postgresql, build it, test it, and apply its
first migration against a real PostgreSQL server.

.DESCRIPTION
MMCA.Common ships PostgreSQL as a first-class engine (ADR-113) and proves the provider with its own
Testcontainers tier. What that tier cannot prove is that a CONSUMER shaped for PostgreSQL comes out
of the scaffold whole: the engine reaches a template through two derived symbols, one marker label
per branch, three injected package ids and a whole-file appsettings variant, and every one of those
can half-apply into a solution that still generates.

So this script is the consumer half, and it is deliberately NOT a fourth case inside smoke.ps1. The
smoke run is the template's correctness gate and has to stay fast and Docker-free; this one wants a
server, which is a different job on a different runner.

Three things are proved here and nowhere else:

  1. The postgresql shape GENERATES: no SQL Server spelling survives anywhere in the tree, and the
     PostgreSQL spellings that replaced them are actually present.
  2. It COMPILES and its tests pass in package mode against the released MMCA.Common, which is the
     build a local source-mode run can never stand in for.
  3. Its first migration APPLIES to a real server. That is the only step that exercises the SQL the
     framework EMITS: the outbox's partial-index predicates, the soft-delete filter's boolean
     comparison and the UTC timestamp mapping all build a valid EF model and are rejected by
     PostgreSQL. Nothing short of a server sees them.

.PARAMETER WorkPath
Scratch directory for the generated solution. Defaults to a temp folder.

.PARAMETER ConnectionString
An Npgsql connection string for a reachable PostgreSQL server. When present the generated migration
is APPLIED to it; when absent the migration is still created (which is most of the provider surface)
and the apply step is skipped, so the script is runnable on a workstation with no server.

.PARAMETER SkipTests
Build only. The generated tests need no database, so there is rarely a reason to pass this.
#>
[CmdletBinding()]
param(
    [string] $WorkPath,
    [string] $ConnectionString,
    [switch] $SkipTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$artifacts = Join-Path $repoRoot 'artifacts'

# Deliberately shares no substring with the seed's own names, exactly as smoke.ps1's cases do: a
# rename that half-applies shows up as a leftover seed token rather than as something that happens
# to match. The two upper-case forms below are what the design-time factory's environment variable
# is renamed to (appShortNameUpper + moduleUpper), and that variable is how the apply step points
# the factory at the server this script was handed.
$appName = 'Contoso.Depot'
$moduleName = 'Orders'
$aggregateName = 'Order'
$connectionEnvVar = 'DEPOT_ORDERS_SQL'
$migrationsProjectName = "$appName.Migrations.PostgreSQL.$moduleName"

if (-not $WorkPath) {
    $WorkPath = Join-Path ([IO.Path]::GetTempPath()) 'mmca-pg'
}
if (Test-Path $WorkPath) { Remove-Item -Recurse -Force $WorkPath }
New-Item -ItemType Directory -Path $WorkPath -Force | Out-Null

# Canonicalize the case. On Windows a path typed as C:\Temp\... reaches MSBuild verbatim while a
# child process that inherits the working directory gets the on-disk C:\temp\..., and the two spelled
# differently in one build make Roslyn miss the .editorconfig: every relaxed StyleCop rule comes back
# as an error in a tree that built clean a moment earlier. Get-Item answers with the real casing.
$WorkPath = (Get-Item $WorkPath).FullName

function Invoke-Step {
    param([string] $Name, [scriptblock] $Body)

    Write-Host ""
    Write-Host "=== PostgreSQL canary: $Name ===" -ForegroundColor Cyan
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
    while ((dotnet new uninstall 2>&1) -match 'MMCA\.Templates') {
        dotnet new uninstall MMCA.Templates | Out-Null
    }
    $nupkg = Get-ChildItem -Path $artifacts -Filter 'MMCA.Templates.*.nupkg' | Select-Object -First 1
    if (-not $nupkg) { throw "Pack produced no MMCA.Templates nupkg in $artifacts" }
    dotnet new install $nupkg.FullName
}

Invoke-Step "Generate $appName (database postgresql)" {
    Push-Location $WorkPath
    try {
        dotnet new mmca-app -n $appName --module $moduleName --aggregate $aggregateName --database postgresql --no-restore
    } finally {
        Pop-Location
    }
}

$appRoot = Join-Path $WorkPath $appName
$slnx = Join-Path $appRoot "$appName.slnx"
if (-not (Test-Path $slnx)) { throw "Generated no $appName.slnx under $appRoot" }

$migrationsProject = Join-Path $appRoot "Source/Hosting/$migrationsProjectName"

# The engine reaches the tree through two symbols, and a rename that applies one and not the other
# leaves a solution pointing at two engines at once: it can still generate, and it cannot compile.
# The folder is renamed by engineName, the factory file by engineNameUpper, so checking both names
# is what tells a clean rename from a half-applied one before the build says anything.
Invoke-Step 'Shape' {
    if (-not (Test-Path $migrationsProject)) {
        throw "No $migrationsProjectName under Source/Hosting. engineName did not rename the migrations project."
    }
    if (-not (Test-Path (Join-Path $migrationsProject 'DesignTimePostgreSQLDbContextFactory.cs'))) {
        throw "No DesignTimePostgreSQLDbContextFactory.cs in ${migrationsProjectName}: engineNameUpper did not rename the design-time factory."
    }

    $props = Join-Path $appRoot 'Directory.Packages.props'
    foreach ($id in @('Npgsql.EntityFrameworkCore.PostgreSQL', 'AspNetCore.HealthChecks.NpgSql', 'Aspire.Hosting.PostgreSQL')) {
        if (-not (Select-String -Path $props -Pattern ([regex]::Escape("Include=`"$id`"")) -Quiet)) {
            throw "Directory.Packages.props pins no $id. The postgresql package swap did not reach it; check the engine alternatives in build/templates/stage.ps1."
        }
    }
    foreach ($id in @('Microsoft.EntityFrameworkCore.SqlServer', 'AspNetCore.HealthChecks.SqlServer', 'Microsoft.EntityFrameworkCore.PostgreSQL', 'AspNetCore.HealthChecks.PostgreSQL')) {
        if (Select-String -Path $props -Pattern ([regex]::Escape("Include=`"$id`"")) -Quiet) {
            throw "Directory.Packages.props still pins $id in a postgresql solution. Either the SQL Server branch survived, or the engineName rename reached an id it must not (there is no Microsoft or HealthChecks package spelled that way for PostgreSQL)."
        }
    }

    Write-Host "  migrations project, design-time factory and all three PostgreSQL package pins are in place"
}

# The mirror of the shape check, and the reason it is not vacuous: an absence check passes just as
# happily when a branch was DELETED as when it was correctly swapped. .md and .resx are exempt for
# the same reasons smoke.ps1 exempts them (prose documents the flags; the localization key set is
# kept whole on purpose), and so are the four copyOnly leaves no rename reaches.
#
# The two SQL Server spellings only, exactly as smoke.ps1's sqlite case sweeps them. 'Sqlite' is
# deliberately not in the pattern: the seed's own comments about the package-id rename name that
# engine in prose, and they ship with the pins they explain whatever the chosen engine is.
Invoke-Step 'No SQL Server spelling survives' {
    $copyOnlyLeaves = @('.editorconfig', 'PageHeading.razor', 'SectionHeading.razor', 'add-module.ps1')

    $offenders = Get-ChildItem -Path $appRoot -Recurse -File -Force |
        Where-Object {
            $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and
            $copyOnlyLeaves -notcontains $_.Name -and
            $_.Extension -ne '.resx' -and
            $_.Extension -ne '.md'
        } |
        ForEach-Object {
            $hit = Select-String -Path $_.FullName -Pattern 'SqlServer|SQLServer' -CaseSensitive -List
            if ($hit) { "$($_.FullName.Substring($appRoot.Length)) line $($hit.LineNumber): $($hit.Line.Trim())" }
        }

    if ($offenders) {
        throw @"
$($offenders.Count) file(s) in a postgresql solution still name SQL Server:
  $($offenders -join "`n  ")
"@
    }

    if (-not (Select-String -Path (Join-Path $appRoot "Source/Modules/$moduleName/$appName.$moduleName.Infrastructure/Persistence/EntityConfiguration/${aggregateName}Configuration.cs") -Pattern 'EntityTypeConfigurationPostgreSQL' -Quiet)) {
        throw "The aggregate's EF configuration does not inherit EntityTypeConfigurationPostgreSQL, so the engine rename produced nothing rather than the right thing."
    }

    Write-Host "  no SqlServer / SQLServer tokens, and the EF configuration is on EntityTypeConfigurationPostgreSQL"
}

Invoke-Step "Build $appName (package mode)" {
    dotnet build $slnx -c Release
}

if (-not $SkipTests) {
    Invoke-Step "Test $appName (no database)" {
        dotnet test --solution $slnx -c Release --no-build --minimum-expected-tests 1
    }
}

# The sample migrations are dropped for every engine but SQL Server (they are SQL Server DDL and
# their model snapshot names the SQL Server context), so this is the migration an adopter of this
# shape runs first, generated exactly the way the generated README tells them to.
Invoke-Step 'Scaffold the first migration' {
    Push-Location $appRoot
    try {
        dotnet ef migrations add InitialCreate `
            --project "Source/Hosting/$migrationsProjectName" `
            --startup-project "Source/Hosting/$migrationsProjectName" `
            --context PostgreSQLDbContext
    } finally {
        Pop-Location
    }
}

if (-not $ConnectionString) {
    Write-Host ""
    Write-Host "PostgreSQL canary: no connection string given, so the apply step is skipped." -ForegroundColor Yellow
    Write-Host "Pass -ConnectionString to run it against a server. Everything else passed."
    return
}

# The step the unit tiers cannot stand in for. Every failure ADR-113 names (a bracketed identifier in
# an index predicate, a boolean compared to an integer, a DateTime whose Kind is not UTC) builds a
# perfectly valid model and is refused HERE, at CREATE INDEX or at the first write.
Invoke-Step 'Apply the migration to a real server' {
    Push-Location $appRoot
    try {
        $previous = [Environment]::GetEnvironmentVariable($connectionEnvVar)
        [Environment]::SetEnvironmentVariable($connectionEnvVar, $ConnectionString)
        try {
            dotnet ef database update `
                --project "Source/Hosting/$migrationsProjectName" `
                --startup-project "Source/Hosting/$migrationsProjectName" `
                --context PostgreSQLDbContext
        } finally {
            [Environment]::SetEnvironmentVariable($connectionEnvVar, $previous)
        }
    } finally {
        Pop-Location
    }
}

Write-Host ""
Write-Host "PostgreSQL canary passed: generated, built, tested, migrated." -ForegroundColor Green
