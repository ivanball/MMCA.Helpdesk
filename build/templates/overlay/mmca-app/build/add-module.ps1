<#
.SYNOPSIS
Adds a business module to this solution and performs every wire-up dotnet new cannot.

.DESCRIPTION
`dotnet new mmca-module` lays down eight projects and then PRINTS seven edits it cannot make,
because a template has no way to reach into files it did not generate. This script makes them.

    pwsh build/add-module.ps1 -Name Orders -Aggregate Order

Everything it needs it discovers at run time from the tree it is standing in: the solution file,
the app's root namespace, the module that is already here, the relational engine this solution
runs on, and the host / test projects it has to patch. Nothing about this app is baked into it,
which is why it is shipped verbatim (the scaffold copies it with no token replacement at all) and
why it starts by refusing to run from anywhere but the solution root.

HOW THE ENGINE IS DETECTED. A module is not engine-neutral: its EF configurations inherit an
engine-specific configuration base and its migrations project references an engine-specific
provider package, so a module built for the wrong engine does not merely look wrong, it does not
compile in the solution it was added to. The engine is read from the API host's own
appsettings.json, from the key spelling inside its top-level ConnectionStrings section
(SQLServerConnectionString or SqliteConnectionString). That file was picked over the alternatives
because it is the file this script also WRITES: detecting from the same place the new data source
is written into is what makes the two impossible to disagree. The existing migrations project
folder (<App>.Migrations.<Engine>.<Module>) is then cross-checked against it, and a disagreement
stops the run rather than adding a project half the solution cannot use. Pass -Database to
override both, which is the answer for a solution that has since grown a second engine.

What it does, in order. Every step is anchored on something the scaffold generated, and a missing
anchor stops the run with the manual edit to make instead, so a half-wired solution is never the
outcome of a silent skip:

  0. preflight: one solution file here, the engine, the module name is free, the SDK is on PATH
  1. dotnet new mmca-module, with the engine and the shape flags passed through
  2. add the eight new projects to the solution
  3. web host project references (the module API and its migrations project)
  4. architecture-test project references (all five layers)
  5. Directory.Build.props: the identifier-alias link
  6. the architecture map: five lines, one per layer
  7. the web host: the module assembly discovery scans, and its AddErrorResources call
  8. the frozen wire contract: the new module's integration event joins ExpectedContract
  9. the orchestration host, when the solution has one: the module's own database and its
     data-source routing (a server database resource, or a second file for SQLite)
 10. the web host's appsettings.json: enable the module, one data source per module, pin the outbox
 11. the module's first migration (skip with -SkipMigration)

Every step also detects work it already did and skips it with a note, so a run that died at step 7
can be fixed and rerun. Starting over with a name that is already under Source/Modules is refused
at step 0 rather than half applied.

.PARAMETER Name
The module, plural PascalCase (Orders, Billing, Reservations). Names the folder, the eight
projects, and the namespaces. Passed to the template as -n.

.PARAMETER Aggregate
The module's aggregate root, singular PascalCase (Order, Invoice, Reservation). Passed as
--aggregate.

.PARAMETER Child
Renames the aggregate's child entity, singular PascalCase. The generated type is
<Aggregate><Child>, so -Aggregate Order -Child Item produces OrderItem with Add / Edit / Remove
slices and /items routes. Passed as --child. Ignored under -Flat.

.PARAMETER Flat
Generate no child collection at all. Passed as --flat.

.PARAMETER NoStatus
Generate no status axis. Passed as --no-status.

.PARAMETER NoOwner
Generate no owning-user property. Passed as --no-owner.

.PARAMETER NoDescription
Generate no long-text property. Passed as --no-description.

.PARAMETER Title
Renames the aggregate's main text property (Name, Subject, CustomerName). Passed straight through
as --title, and named the same way here so the mapping is one to one.

.PARAMETER EventVerb
Names the creation integration event's verb, past tense PascalCase (Created, Placed, Booked).
Passed as --event-verb.

.PARAMETER Database
Overrides the engine this solution is read to be running on: sqlserver or sqlite. Leave it off and
the engine is detected (see HOW THE ENGINE IS DETECTED above), which is right for every solution
the scaffold produced. Pass it when a solution has grown a second engine and the detection can no
longer answer for the module you are adding. Passed to the template as --database.

.PARAMETER SkipMigration
Do not run `dotnet ef migrations add`. The command is printed instead. The script also degrades to
printing it on its own when the dotnet-ef tool is not installed, so pass this only when you want to
create the migration later on purpose. On SQLite that is a decision worth making deliberately: a
SQLite data source that names a migrations assembly is MIGRATED at startup rather than created
outright, so until the migration exists the host starts against an empty database.

.EXAMPLE
pwsh build/add-module.ps1 -Name Orders -Aggregate Order -Child Item

.EXAMPLE
pwsh build/add-module.ps1 -Name Products -Aggregate Product -Flat -NoStatus -NoOwner -Title Name

.EXAMPLE
pwsh build/add-module.ps1 -Name Orders -Aggregate Order -EventVerb Placed -SkipMigration

.NOTES
Run with -? for the full option list. Two things this deliberately does not do are listed in the
summary it prints when it finishes.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9]*$')]
    [string] $Name,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z][A-Za-z0-9]*$')]
    [string] $Aggregate,

    [ValidatePattern('^[A-Za-z][A-Za-z0-9]*$')]
    [string] $Child,

    [switch] $Flat,
    [switch] $NoStatus,
    [switch] $NoOwner,
    [switch] $NoDescription,

    [ValidatePattern('^[A-Za-z][A-Za-z0-9]*$')]
    [string] $Title,

    [ValidatePattern('^[A-Za-z][A-Za-z0-9]*$')]
    [string] $EventVerb,

    [ValidateSet('sqlserver', 'sqlite')]
    [string] $Database,

    [switch] $SkipMigration
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# PowerShell 7.4 made a native command's non-zero exit honor $ErrorActionPreference. That would turn
# the three deliberate PROBES below (is the template installed, is dotnet-ef here, is this a git
# working tree) into terminating errors, and a probe whose whole job is to answer "no" gracefully
# must not stop the run. Every call that does matter is checked through Invoke-Native instead.
$PSNativeCommandUseErrorActionPreference = $false

$script:Applied = [System.Collections.Generic.List[string]]::new()
$script:Skipped = [System.Collections.Generic.List[string]]::new()

function Write-Step {
    param([string] $Text)
    Write-Host ""
    Write-Host "=== $Text ===" -ForegroundColor Cyan
}

function Write-Applied {
    param([string] $Text)
    Write-Host "  $Text" -ForegroundColor Green
    $script:Applied.Add($Text)
}

function Write-Skipped {
    param([string] $Text)
    Write-Host "  already done: $Text" -ForegroundColor DarkGray
    $script:Skipped.Add($Text)
}

# Native commands do not raise, they set an exit code, and $ErrorActionPreference does not see it.
# Every dotnet call in this script matters, so none of them may fail quietly.
function Invoke-Native {
    param([string] $What, [scriptblock] $Body)

    & $Body
    if ($LASTEXITCODE -ne 0) {
        throw "$What failed with exit code $LASTEXITCODE."
    }
}

# ---- file editing -------------------------------------------------------------------------------
# Anchored text edits throughout, never a parse-and-reserialize round trip. Every file touched here
# was written by the scaffold and is still formatted the way the scaffold wrote it, and an adopter
# reading `git diff` after this run should see the lines that were added and nothing else. A
# round trip through an object model reflows the whole file and buries the four lines that matter.

function Get-TextDocument {
    param([string] $Path)

    $text = Get-Content -Path $Path -Raw
    # Whatever the file already uses. Mixing terminators inside one file is an analyzer error in
    # this solution (IDE0055), so an edit must never introduce a second style.
    $newline = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }

    return @{
        Path    = $Path
        Newline = $newline
        Lines   = [System.Collections.Generic.List[string]] ($text -split "`r?`n")
    }
}

function Save-TextDocument {
    param([hashtable] $Document)

    Set-Content -Path $Document.Path -Value ($Document.Lines -join $Document.Newline) -NoNewline
}

# The workhorse for steps 3 to 8. Finds the LAST line matching $Anchor and inserts after it, giving
# every inserted line the anchor's own indentation plus whatever relative indentation it carries.
# Last rather than first so that adding a third module lands beside the second rather than between
# the first and the second, which is where a reader looks for it.
function Add-AfterAnchor {
    param(
        [string] $Path,
        [string] $Anchor,
        [string[]] $Insert,
        [string] $AlreadyApplied,
        [string] $Description,
        [string] $Manual,
        # Overrides the anchor line's own indentation. Needed wherever the anchor is a CONTINUATION
        # line of a multi-line element (Directory.Build.props anchors on the third line of a three
        # line Compile item), where copying its indentation would indent the new element to match
        # the middle of the old one.
        [string] $Indent
    )

    if (-not (Test-Path $Path)) {
        throw "$Description : no file at $Path. Do this by hand instead:`n$Manual"
    }

    $document = Get-TextDocument $Path

    # Idempotence before anchoring: a rerun after a mid-way failure must not double the lines, and
    # the check has to come first because the anchor is still there on a second pass.
    if ($AlreadyApplied -and (($document.Lines -join "`n") -match $AlreadyApplied)) {
        Write-Skipped $Description
        return
    }

    $at = -1
    for ($i = 0; $i -lt $document.Lines.Count; $i++) {
        if ($document.Lines[$i] -match $Anchor) { $at = $i }
    }

    if ($at -lt 0) {
        throw @"
$Description : no line matching /$Anchor/ in $Path.
The scaffold's shape moved, or this file was already reworked by hand, so the edit cannot be placed
safely. Nothing was written. Make it yourself:

$Manual
"@
    }

    # ContainsKey rather than a null test: an unbound [string] parameter arrives as an EMPTY string,
    # not as $null, and empty is a legitimate override (column zero).
    $base = if ($PSBoundParameters.ContainsKey('Indent')) { $Indent } else { [regex]::Match($document.Lines[$at], '^[ \t]*').Value }
    # A blank separator line stays blank: trailing whitespace is an analyzer error here.
    $indented = @($Insert | ForEach-Object { if ($_) { $base + $_ } else { '' } })

    $document.Lines.InsertRange($at + 1, [string[]] $indented)
    Save-TextDocument $document
    Write-Applied $Description
}

# The mirror of Add-AfterAnchor, and it exists for exactly one step: the orchestration host's
# data-source routing, where the order of the chained calls is SEMANTIC rather than cosmetic. Every
# data-source call also rewrites the host's top-level connection string, so the last one in the chain
# decides which module is the solution's Default source. That has to stay the FIRST module: the
# top-level connection in appsettings names its database, the outbox is pinned to it, and its
# migrations are the ones scaffolded with the Default-source-only framework tables in them. Appending
# would silently hand the role to whichever module was added most recently, whose migrations do not
# carry those tables, and EF refuses to migrate a database whose model has pending changes.
function Add-BeforeAnchor {
    param(
        [string] $Path,
        [string] $Anchor,
        [string[]] $Insert,
        [string] $AlreadyApplied,
        [string] $Description,
        [string] $Manual
    )

    if (-not (Test-Path $Path)) {
        throw "$Description : no file at $Path. Do this by hand instead:`n$Manual"
    }

    $document = Get-TextDocument $Path

    if ($AlreadyApplied -and (($document.Lines -join "`n") -match $AlreadyApplied)) {
        Write-Skipped $Description
        return
    }

    # FIRST match, not last: inserting above the earliest call is what leaves every earlier module's
    # call after this one, and the very first module's call last of all.
    $at = -1
    for ($i = 0; $i -lt $document.Lines.Count; $i++) {
        if ($document.Lines[$i] -match $Anchor) { $at = $i; break }
    }

    if ($at -lt 0) {
        throw @"
$Description : no line matching /$Anchor/ in $Path.
The scaffold's shape moved, or this file was already reworked by hand, so the edit cannot be placed
safely. Nothing was written. Make it yourself:

$Manual
"@
    }

    $base = [regex]::Match($document.Lines[$at], '^[ \t]*').Value
    $indented = @($Insert | ForEach-Object { if ($_) { $base + $_ } else { '' } })

    $document.Lines.InsertRange($at, [string[]] $indented)
    Save-TextDocument $document
    Write-Applied $Description
}

# ---- appsettings.json ---------------------------------------------------------------------------
# Brace counting rather than a JSON parse. It is safe HERE and only here: this file is generated,
# two levels deep, and none of its string values contain a brace. It is what lets the edits land as
# added lines in an otherwise untouched file.

function Get-JsonObjectRange {
    param([hashtable] $Document, [string] $Key, [switch] $Optional)

    # TOP-LEVEL keys only. A nested object can carry the same name as a root one, and a per-tenant
    # "DataSources" override under the tenancy section is exactly that case. Rewriting the nested
    # object instead of the root one produces a file that is still valid JSON and silently wrong, so
    # the search tracks brace depth and only accepts a match sitting directly in the root object.
    $openPattern = '^\s*"' + [regex]::Escape($Key) + '"\s*:\s*\{'
    $open = -1
    $depth = 0
    for ($i = 0; $i -lt $Document.Lines.Count; $i++) {
        if ($depth -eq 1 -and $Document.Lines[$i] -match $openPattern) { $open = $i; break }
        $depth += ([regex]::Matches($Document.Lines[$i], '\{')).Count
        $depth -= ([regex]::Matches($Document.Lines[$i], '\}')).Count
    }

    if ($open -lt 0) {
        if ($Optional) { return $null }
        throw "appsettings.json has no top-level `"$Key`" section. Add it by hand (see the printed instructions from dotnet new mmca-module)."
    }

    $depth = 0
    for ($i = $open; $i -lt $Document.Lines.Count; $i++) {
        $depth += ([regex]::Matches($Document.Lines[$i], '\{')).Count
        $depth -= ([regex]::Matches($Document.Lines[$i], '\}')).Count
        if ($depth -eq 0) { return @{ Open = $open; Close = $i } }
    }

    throw "appsettings.json's `"$Key`" section is never closed. Fix the file before rerunning."
}

function Get-RootObjectRange {
    param([hashtable] $Document)

    $open = -1
    $close = -1
    for ($i = 0; $i -lt $Document.Lines.Count; $i++) {
        if ($open -lt 0 -and $Document.Lines[$i].Trim() -eq '{') { $open = $i }
        if ($Document.Lines[$i].Trim() -eq '}') { $close = $i }
    }
    if ($open -lt 0 -or $close -le $open) {
        throw "appsettings.json is not a single JSON object. Fix the file before rerunning."
    }
    return @{ Open = $open; Close = $close }
}

# Inserts a member as the LAST one of an object, giving the member that used to be last the comma
# it now needs. JSON has no trailing comma, so an append is always two edits, not one.
function Add-JsonMember {
    param([hashtable] $Document, [hashtable] $Range, [string[]] $Member)

    $previous = $Range.Close - 1
    while ($previous -gt $Range.Open -and -not $Document.Lines[$previous].Trim()) { $previous-- }

    if ($previous -gt $Range.Open) {
        $trimmed = $Document.Lines[$previous].TrimEnd()
        if (-not $trimmed.EndsWith(',')) { $Document.Lines[$previous] = $trimmed + ',' }
    }

    $Document.Lines.InsertRange($Range.Close, [string[]] $Member)
}

function Get-IndentOf {
    param([hashtable] $Document, [int] $Line)
    return [regex]::Match($Document.Lines[$Line], '^[ \t]*').Value
}

# ---- 0. preflight -------------------------------------------------------------------------------
Write-Step 'Preflight'

$root = (Get-Location).Path

# The one thing that cannot be discovered from anywhere else. Every path below is relative to the
# solution root, so running from a subfolder would scatter eight projects into it and wire nothing.
$solutions = @(Get-ChildItem -Path $root -Filter '*.slnx' -File)
if ($solutions.Count -ne 1) {
    throw @"
Found $($solutions.Count) *.slnx files in $root, expected exactly one.
Run this from your solution root:

    cd <the folder holding YourApp.slnx>
    pwsh build/add-module.ps1 -Name $Name -Aggregate $Aggregate
"@
}

$solution = $solutions[0]
# The app's root namespace IS the solution file's name: the scaffold names them together, and every
# project, namespace and assembly below is built from it.
$app = [IO.Path]::GetFileNameWithoutExtension($solution.Name)
$appShort = $app.Split('.')[-1]
$appShortLower = $appShort.ToLowerInvariant()
$nameLower = $Name.ToLowerInvariant()

$modulesRoot = Join-Path $root 'Source/Modules'
if (-not (Test-Path $modulesRoot)) {
    throw "No Source/Modules folder under $root. This does not look like a solution generated by mmca-app."
}

$existingModules = @(Get-ChildItem -Path $modulesRoot -Directory | ForEach-Object { $_.Name })
if ($existingModules.Count -eq 0) {
    throw "Source/Modules is empty, so there is no existing module to anchor the wire-up edits on. Add the first module with the mmca-app scaffold, not with this script."
}

# The module that was here first. It owns every anchor this script edits beside, and it is the one
# the outbox gets pinned to in step 9.
$firstModule = $existingModules[0]

$hostsRoot = Join-Path $root 'Source/Hosts'
$hostingRoot = Join-Path $root 'Source/Hosting'
$archRoot = Join-Path $root 'Tests/Architecture'

function Find-SingleDirectory {
    param(
        [string] $Parent,
        [string] $Pattern,
        [string] $What,
        # Zero hits is a legitimate answer for a project the solution may simply not have. Only the
        # orchestration project is optional today: a solution scaffolded without one has no
        # orchestrator directory at all, and refusing to run there would make this script useless in
        # exactly the shape it is most useful in.
        [switch] $Optional
    )

    if (-not (Test-Path $Parent)) {
        if ($Optional) { return $null }
        throw "No $Parent folder. Cannot locate the $What project; wire the module up by hand (see the instructions dotnet new mmca-module prints)."
    }
    # Depth one on purpose: the UI host lives one level further down under Hosts/UI, and matching it
    # here would make the web host ambiguous.
    $hits = @(Get-ChildItem -Path $Parent -Directory | Where-Object { $_.Name -like $Pattern })
    if ($Optional -and $hits.Count -eq 0) { return $null }
    if ($hits.Count -ne 1) {
        throw "Found $($hits.Count) directories matching '$Pattern' under $Parent, expected exactly one ($What). Wire the module up by hand."
    }
    return $hits[0].FullName
}

$webHostDir = Find-SingleDirectory -Parent $hostsRoot -Pattern '*.Web' -What 'web API host'
$appHostDir = Find-SingleDirectory -Parent $hostingRoot -Pattern '*AppHost*' -What 'orchestration host' -Optional
$archTestsDir = Find-SingleDirectory -Parent $archRoot -Pattern '*.Architecture.Tests' -What 'architecture-fitness test'

$webHostName = [IO.Path]::GetFileName($webHostDir)
$archTestsName = [IO.Path]::GetFileName($archTestsDir)

$webCsproj = Join-Path $webHostDir "$webHostName.csproj"
$webProgram = Join-Path $webHostDir 'Program.cs'
$webSettings = Join-Path $webHostDir 'appsettings.json'
$appHostProgram = if ($appHostDir) { Join-Path $appHostDir 'Program.cs' } else { $null }
$archCsproj = Join-Path $archTestsDir "$archTestsName.csproj"
$buildProps = Join-Path $root 'Directory.Build.props'

$requiredFiles = @($webCsproj, $webProgram, $webSettings, $archCsproj, $buildProps)
if ($appHostProgram) { $requiredFiles += $appHostProgram }

foreach ($required in $requiredFiles) {
    if (-not (Test-Path $required)) {
        throw "Expected $required. The scaffold's layout moved; wire the module up by hand (dotnet new mmca-module prints every step)."
    }
}

# The map may be its own file or a type inside the test file, depending on how the solution was
# scaffolded and on what the adopter has done since. Look for the file first, then for the type.
$mapCandidates = @(Get-ChildItem -Path $archTestsDir -Recurse -File -Filter '*ArchitectureMap.cs')
if ($mapCandidates.Count -eq 0) {
    $mapCandidates = @(Get-ChildItem -Path $archTestsDir -Recurse -File -Filter '*.cs' |
        Where-Object { (Get-Content $_.FullName -Raw) -match 'ArchitectureMapBase' })
}
if ($mapCandidates.Count -ne 1) {
    throw "Found $($mapCandidates.Count) architecture-map files under $archTestsDir, expected exactly one. Add the five Module(...) lines by hand."
}
$archMap = $mapCandidates[0].FullName

# ---- which engine this solution runs on ----------------------------------------------------------
# Two spellings, because the framework uses two and both are real. The first names the migrations
# project (its folder, its assembly, its namespace) and the provider package; the second names the
# DbContext, the entity-configuration base and every settings key. SQLite happens to spell them the
# same, which is precisely why they are carried as two values rather than derived from one another.
$engineSpellings = @{
    'sqlserver' = @{ Name = 'SqlServer'; Upper = 'SQLServer' }
    'sqlite'    = @{ Name = 'Sqlite';    Upper = 'Sqlite' }
}

# Read from the top-level ConnectionStrings section rather than from anywhere in the file: a
# per-tenant override further up carries the same key spelling, and a nested hit would answer for a
# section this script does not route. Ordinal comparison, so the shorter key cannot match inside the
# longer one.
$settingsProbe = Get-TextDocument $webSettings
$probeRange = Get-JsonObjectRange -Document $settingsProbe -Key 'ConnectionStrings'
$probeLines = $settingsProbe.Lines[$probeRange.Open..$probeRange.Close]

$detectedEngines = @($engineSpellings.Keys | Sort-Object | Where-Object {
    $key = '"' + $engineSpellings[$_].Upper + 'ConnectionString"'
    @($probeLines | Where-Object { $_.Contains($key) }).Count -gt 0
})

if ($Database) {
    $engineChoice = $Database
} elseif ($detectedEngines.Count -eq 1) {
    $engineChoice = $detectedEngines[0]
} else {
    throw @"
Could not tell which relational engine this solution runs on. The top-level ConnectionStrings
section of $webHostName/appsettings.json names $($detectedEngines.Count) engine connection string
$(if ($detectedEngines) { "($($detectedEngines -join ', '))" } else { '(none)' }), and exactly one is
needed to decide which configuration base the new module's EF configurations inherit and which
provider its migrations project references. Nothing was written.

Say it explicitly and rerun:

    pwsh build/add-module.ps1 -Name $Name -Aggregate $Aggregate -Database sqlserver
    pwsh build/add-module.ps1 -Name $Name -Aggregate $Aggregate -Database sqlite
"@
}

$engineName = $engineSpellings[$engineChoice].Name
$engineUpper = $engineSpellings[$engineChoice].Upper
$dbContextName = "${engineUpper}DbContext"
$connectionKey = "${engineUpper}ConnectionString"
$migrationsAssemblyKey = "${engineUpper}MigrationsAssembly"

# Cross-check against what is on disk. The settings file and the migrations projects have to agree:
# adding a Sqlite-shaped module to a solution whose every other migrations project is SQL Server
# leaves a project referencing a provider package the solution does not pin, and an aggregate whose
# configuration inherits a base its host never registered. Neither is a build this script should
# start. An explicit -Database is the adopter saying they know; it warns instead.
$existingMigrationDirs = @(
    if (Test-Path $hostingRoot) {
        Get-ChildItem -Path $hostingRoot -Directory | Where-Object { $_.Name -like "$app.Migrations.*" }
    })
$agreeingMigrationDirs = @($existingMigrationDirs | Where-Object { $_.Name -like "$app.Migrations.$engineName.*" })

if ($existingMigrationDirs.Count -gt 0 -and $agreeingMigrationDirs.Count -eq 0) {
    $mismatch = @"
This solution's settings say $engineChoice, but none of its $($existingMigrationDirs.Count) migrations project(s) is
named for that engine:
  $(($existingMigrationDirs | ForEach-Object { $_.Name }) -join "`n  ")
A module generated for the wrong engine inherits a configuration base and references a provider
package the rest of the solution does not use, so it will not build here.
"@
    if ($Database) {
        Write-Warning $mismatch
    } else {
        throw "$mismatch`nFix the settings file, or say which engine you meant with -Database sqlserver / -Database sqlite. Nothing was written."
    }
}

$migrationsProject = "Source/Hosting/$app.Migrations.$engineName.$Name"

if ($existingModules -contains $Name) {
    throw @"
Source/Modules/$Name already exists. Nothing was written.
Generating over files you may have edited is not a decision this script gets to make for you.

Pick another name, or, to start this module over, delete these three trees and rerun:
    Source/Modules/$Name
    Tests/Modules/$Name
    $migrationsProject
The wire-up edits themselves need no undoing: every step below detects its own work and skips it,
so a rerun after a failure part way through picks up exactly where it stopped.
"@
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The dotnet SDK is not on PATH. Install .NET 10 and rerun; every step below shells out to it."
}
Invoke-Native 'dotnet --version' { dotnet --version | Out-Null }

# A dirty tree is not an error: an adopter may well be adding a module on top of other work. It is
# worth saying out loud, because `git diff` right after this run is the fastest way to review (or
# revert) the six existing files it is about to touch. Not every generated solution is a git
# repository, and one that is not is not a problem either.
if (Get-Command git -ErrorAction SilentlyContinue) {
    $gitStatus = & git status --porcelain 2>$null
    if ($LASTEXITCODE -eq 0 -and $gitStatus) {
        Write-Warning "Your working tree has uncommitted changes. This script edits six existing files; commit or stash first if you want its diff on its own."
    }
}

Write-Host "  solution      $($solution.Name)"
Write-Host "  app namespace $app (short name $appShort)"
Write-Host "  engine        $engineChoice $(if ($Database) { '(you passed -Database)' } else { "(read from $webHostName/appsettings.json)" })"
Write-Host "  existing      $($existingModules -join ', ')"
Write-Host "  adding        $Name (aggregate $Aggregate)"
Write-Host "  web host      $webHostName"
Write-Host "  app host      $(if ($appHostDir) { [IO.Path]::GetFileName($appHostDir) } else { 'none (this solution has no orchestration project)' })"
Write-Host "  arch tests    $archTestsName"
Write-Host "  arch map      $([IO.Path]::GetFileName($archMap))"

# ---- 1. generate --------------------------------------------------------------------------------
Write-Step "Generate module $Name"

$templateList = & dotnet new list mmca-module 2>&1 | Out-String
if ($templateList -notmatch 'mmca-module') {
    throw @"
The mmca-module template is not installed, so there is nothing to generate. Install the pack:

    dotnet new install MMCA.Templates
"@
}

# The engine goes first and always, not only when it is the non-default one: this script's whole
# claim is that the module it generates matches the solution it lands in, and a flag that is only
# sometimes passed makes that claim depend on the template's default staying what it is today.
$templateArgs = @('-n', $Name, '--app', $app, '--aggregate', $Aggregate, '--database', $engineChoice)
if ($Child) { $templateArgs += @('--child', $Child) }
if ($Title) { $templateArgs += @('--title', $Title) }
if ($EventVerb) { $templateArgs += @('--event-verb', $EventVerb) }
if ($Flat) { $templateArgs += '--flat' }
if ($NoStatus) { $templateArgs += '--no-status' }
if ($NoOwner) { $templateArgs += '--no-owner' }
if ($NoDescription) { $templateArgs += '--no-description' }

Write-Host "  dotnet new mmca-module $($templateArgs -join ' ')"
Write-Host "  (the template prints the wire-up steps it cannot perform; this script performs them, so that list is FYI)" -ForegroundColor DarkGray
Invoke-Native 'dotnet new mmca-module' { dotnet new mmca-module @templateArgs }

$moduleRoot = Join-Path $modulesRoot $Name
if (-not (Test-Path $moduleRoot)) {
    throw "dotnet new mmca-module reported success but produced no Source/Modules/$Name. Nothing was wired."
}
Write-Applied "generated Source/Modules/$Name, Tests/Modules/$Name and $migrationsProject"

# ---- 2. solution --------------------------------------------------------------------------------
Write-Step 'Add the projects to the solution'

# Get-ChildItem rather than a shell glob: PowerShell hands an unexpanded '*' straight to dotnet,
# which then reports "the file does not exist" for a path that plainly does.
$newProjects = @(
    Get-ChildItem -Path (Join-Path $modulesRoot $Name) -Recurse -File -Filter '*.csproj'
    Get-ChildItem -Path (Join-Path $root "Tests/Modules/$Name") -Recurse -File -Filter '*.csproj'
    Get-ChildItem -Path (Join-Path $root $migrationsProject) -Recurse -File -Filter '*.csproj'
) | ForEach-Object { $_.FullName }

# Five layers, two test projects, one migrations project. A different count means the template
# generated a shape this script does not know how to wire, and adding a partial set to the solution
# is worse than stopping.
if ($newProjects.Count -ne 8) {
    throw "Expected 8 generated projects for $Name, found $($newProjects.Count):`n  $($newProjects -join "`n  ")"
}

$solutionText = Get-Content $solution.FullName -Raw
if ($solutionText -match ([regex]::Escape("Modules/$Name/")) -or $solutionText -match ([regex]::Escape("Modules\$Name\"))) {
    Write-Skipped "$($solution.Name) already lists the $Name projects"
} else {
    Invoke-Native 'dotnet sln add' { dotnet sln $solution.FullName add @newProjects | Out-Null }
    Write-Applied "added 8 projects to $($solution.Name)"
}

# ---- 3. web host project references -------------------------------------------------------------
Write-Step 'Web host project references'

# Both are needed and for different reasons: the API reference is what makes the module's
# controllers and its error resources resolve in the host, and the migrations reference is what puts
# the module's migrations assembly in the host's output so EF can find it at startup.
Add-AfterAnchor `
    -Path $webCsproj `
    -Anchor '<ProjectReference\s+Include="[^"]*[\\/]Modules[\\/][^"]*\.API\.csproj"' `
    -Insert @(
        "<ProjectReference Include=`"..\..\Hosting\$app.Migrations.$engineName.$Name\$app.Migrations.$engineName.$Name.csproj`" />"
        "<ProjectReference Include=`"..\..\Modules\$Name\$app.$Name.API\$app.$Name.API.csproj`" />"
    ) `
    -AlreadyApplied ([regex]::Escape("$app.$Name.API.csproj")) `
    -Description "$webHostName.csproj references $app.$Name.API and its migrations project" `
    -Manual "Add to $webHostName.csproj, in the ItemGroup that already holds the ProjectReference elements:`n    <ProjectReference Include=`"..\..\Hosting\$app.Migrations.$engineName.$Name\$app.Migrations.$engineName.$Name.csproj`" />`n    <ProjectReference Include=`"..\..\Modules\$Name\$app.$Name.API\$app.$Name.API.csproj`" />"

# ---- 4. architecture-test project references ----------------------------------------------------
Write-Step 'Architecture-test project references'

# All five layers, because the map added in step 6 names a type from each one and a type reference
# needs the assembly on the compile line.
$layerRefs = @('Domain', 'Application', 'Infrastructure', 'Shared', 'API') | ForEach-Object {
    "<ProjectReference Include=`"..\..\..\Source\Modules\$Name\$app.$Name.$_\$app.$Name.$_.csproj`" />"
}

Add-AfterAnchor `
    -Path $archCsproj `
    -Anchor '<ProjectReference\s+Include="[^"]*[\\/]Modules[\\/]' `
    -Insert $layerRefs `
    -AlreadyApplied ([regex]::Escape("$app.$Name.Domain.csproj")) `
    -Description "$archTestsName.csproj references all five $Name layers" `
    -Manual "Add to $archTestsName.csproj:`n    $($layerRefs -join "`n    ")"

# ---- 5. identifier alias ------------------------------------------------------------------------
Write-Step 'Identifier-alias link'

# The alias file declares `global using <Aggregate>IdentifierType = ...`. It lives in the module's
# Shared project and is LINKED into every other project in the solution, which is what makes the
# alias mean the same thing in the domain, the handlers and the controller. Without this block the
# alias is visible only inside the project that declares it, and the module does not compile.
$aliasFile = "$app.$Name.GlobalUsings.IdentifierType.cs"
$aliasPath = Join-Path $moduleRoot "$app.$Name.Shared/$aliasFile"

if (-not (Test-Path $aliasPath)) {
    Write-Skipped "no $aliasFile in the generated module, so there is no alias to link"
} else {
    # The anchor is the LAST line of a three line element, so its indentation is the continuation
    # indentation. The new element needs the indentation of the FIRST line instead.
    $aliasIndent = ([regex]::Match((Get-Content $buildProps -Raw), '(?m)^([ \t]*)<Compile Include="\$\(MSBuildThisFileDirectory\)')).Groups[1].Value
    if (-not $aliasIndent) { $aliasIndent = '    ' }

    Add-AfterAnchor `
        -Path $buildProps `
        -Indent $aliasIndent `
        -Anchor "Condition=`"'\`$\(MSBuildProjectName\)' != '[^']*\.Shared'`"\s*/>" `
        -Insert @(
            "<Compile Include=`"`$(MSBuildThisFileDirectory)Source\Modules\$Name\$app.$Name.Shared\$aliasFile`""
            "         Link=`"GlobalUsings\$aliasFile`""
            "         Condition=`"'`$(MSBuildProjectName)' != '$app.$Name.Shared'`" />"
        ) `
        -AlreadyApplied ([regex]::Escape($aliasFile)) `
        -Description "Directory.Build.props links $aliasFile into every project" `
        -Manual "Add to Directory.Build.props, beside the existing <Compile Include ... Link ...> block:`n    <Compile Include=`"`$(MSBuildThisFileDirectory)Source\Modules\$Name\$app.$Name.Shared\$aliasFile`"`n             Link=`"GlobalUsings\$aliasFile`"`n             Condition=`"'`$(MSBuildProjectName)' != '$app.$Name.Shared'`" />"
}

# ---- 6. architecture map ------------------------------------------------------------------------
Write-Step 'Architecture map'

# A module missing from the map is not a failing test, it is a SILENTLY unenforced one: the layering
# and module-isolation rules iterate the map, so an unregistered assembly is simply never checked.
$mapLines = @(
    ''
    "// $Name module"
    "Module(`"$Name`", Layer.Domain, typeof($app.$Name.Domain.$Name.$Aggregate).Assembly),"
    "Module(`"$Name`", Layer.Application, typeof($app.$Name.Application.ClassReference).Assembly),"
    "Module(`"$Name`", Layer.Infrastructure, typeof($app.$Name.Infrastructure.AssemblyReference).Assembly),"
    "Module(`"$Name`", Layer.Shared, typeof($app.$Name.Shared.$Name.${Aggregate}DTO).Assembly),"
    "Module(`"$Name`", Layer.Api, typeof($app.$Name.API.Controllers.${Name}Controller).Assembly),"
)

Add-AfterAnchor `
    -Path $archMap `
    -Anchor 'Module\("[^"]+",\s*Layer\.Api,' `
    -Insert $mapLines `
    -AlreadyApplied ([regex]::Escape("Module(`"$Name`",")) `
    -Description "$([IO.Path]::GetFileName($archMap)) registers the five $Name layer assemblies" `
    -Manual "Add to $([IO.Path]::GetFileName($archMap)), inside the layer list:`n        $(($mapLines | Where-Object { $_ }) -join "`n        ")"

# ---- 7. host registrations ----------------------------------------------------------------------
Write-Step 'Host registrations'

# The host names the assemblies discovery scans, so a new module's assembly has to join that list:
# an assembly no code path has touched yet is not loaded, and would be silently absent from any
# ambient scan. Without this line the module compiles, ships, and registers nothing.
Add-AfterAnchor `
    -Path $webProgram `
    -Anchor '^\s*using\s+[^;]*\.API;' `
    -Insert @("using $app.$Name.API;") `
    -AlreadyApplied ([regex]::Escape("using $app.$Name.API;")) `
    -Description "Program.cs imports $app.$Name.API" `
    -Manual "Add `"using $app.$Name.API;`" to the top of $webHostName/Program.cs."

Add-AfterAnchor `
    -Path $webProgram `
    -Anchor '^\s*typeof\([A-Za-z0-9_.]+Module\)\.Assembly,' `
    -Insert @("typeof(${Name}Module).Assembly,") `
    -AlreadyApplied ([regex]::Escape("typeof(${Name}Module).Assembly,")) `
    -Description "Program.cs adds ${Name}Module's assembly to module discovery" `
    -Manual "Add `"typeof(${Name}Module).Assembly,`" to the assembly list passed to DiscoverAndRegister in $webHostName/Program.cs."

# The second registration the host owns: the module's error-code translations reach the edge
# localizer, which is what turns a domain error code into a localized ProblemDetails message.
Add-AfterAnchor `
    -Path $webProgram `
    -Anchor '^using\s+[^;]*\.API\.Resources;' `
    -Insert @("using $app.$Name.API.Resources;") `
    -AlreadyApplied ([regex]::Escape("using $app.$Name.API.Resources;")) `
    -Description "Program.cs imports $app.$Name.API.Resources" `
    -Manual "Add `"using $app.$Name.API.Resources;`" to the top of $webHostName/Program.cs."

Add-AfterAnchor `
    -Path $webProgram `
    -Anchor '^\s*services\.AddErrorResources<' `
    -Insert @("services.AddErrorResources<${Name}ErrorResources>();") `
    -AlreadyApplied ([regex]::Escape("AddErrorResources<${Name}ErrorResources>")) `
    -Description "Program.cs calls AddErrorResources<${Name}ErrorResources>()" `
    -Manual "Add `"services.AddErrorResources<${Name}ErrorResources>();`" beside the existing AddErrorResources call in $webHostName/Program.cs."

# ---- 8. the frozen integration-event wire contract -----------------------------------------------
Write-Step 'Integration-event wire contract'

# The new module ships a creation integration event, and the solution's contract test compares the
# LIVE set of events against a committed literal. A new event that is not in that literal is a
# failing test in a file the adopter did not write, on their next test run, so the literal moves with
# the module. Skipped rather than fatal when the class is not there: an adopter is free to delete it.
$contractFiles = @(Get-ChildItem -Path $archTestsDir -Recurse -File -Filter '*.cs' |
    Where-Object { (Get-Content $_.FullName -Raw) -match 'IntegrationEventContractTestsBase' })

if ($contractFiles.Count -ne 1) {
    Write-Skipped "no single IntegrationEventContractTests class under $archTestsName (found $($contractFiles.Count)); nothing to freeze"
} else {
    # The event's own members, in the shape the base prints them. The identifier is always there; the
    # owning user is the one member a shape flag removes, and the base compares members as a set, so
    # the order here only has to be readable.
    $contractMembers = @()
    if (-not $NoOwner) { $contractMembers += 'RequesterUserId:Int32' }
    $contractMembers += "${Aggregate}Id:Int32"

    $verb = if ($EventVerb) { $EventVerb } else { 'Opened' }
    $eventType = "$app.$Name.Shared.$Name.IntegrationEvents.$Aggregate${verb}IntegrationEvent"
    $contractLine = "`"$eventType { $($contractMembers -join ', ') }`","

    Add-AfterAnchor `
        -Path $contractFiles[0].FullName `
        -Anchor 'IntegrationEvent \{[^}]*\}",$' `
        -Insert @($contractLine) `
        -AlreadyApplied ([regex]::Escape($eventType)) `
        -Description "$($contractFiles[0].Name) freezes $Aggregate${verb}IntegrationEvent" `
        -Manual "Add to ExpectedContract in $($contractFiles[0].Name):`n        $contractLine"
}

# ---- 9. the orchestration host: the module's own database ----------------------------------------
Write-Step 'Orchestration host database resource'

# One database per module, not one per solution. Each module database carries its own outbox and
# inbox tables, so two modules migrated into one database collide on them, and per-module databases
# are what keeps extracting a module into its own service a hosting change rather than a rewrite.
#
# The shape of that declaration is where the two engines genuinely differ rather than merely spell
# things differently. A server database is a RESOURCE the orchestrator creates and the host waits
# for, so it takes two edits: declare it, then route it. A SQLite database is a file the provider
# opens in process, so there is no resource, nothing to wait for, and one edit: the routing call
# carries the path itself.
$moduleDbFile = "${appShortLower}_$nameLower.db"

if (-not $appHostProgram) {
    Write-Skipped 'this solution has no orchestration project, so there is nothing to declare here (the DataSources entry below is what routes the module)'
} elseif ($engineChoice -eq 'sqlite') {
    Add-BeforeAnchor `
        -Path $appHostProgram `
        -Anchor '\.WithSqliteDataSource\(' `
        -Insert @(".WithSqliteDataSource(`"$Name`", Path.Combine(builder.AppHostDirectory, `"$moduleDbFile`"))") `
        -AlreadyApplied ([regex]::Escape("WithSqliteDataSource(`"$Name`"")) `
        -Description "the orchestration host routes the $Name data source to its own database file" `
        -Manual "Chain onto the web project in the orchestration host's Program.cs, ABOVE the existing call (the last one wins the Default source, and that has to stay the first module):`n    .WithSqliteDataSource(`"$Name`", Path.Combine(builder.AppHostDirectory, `"$moduleDbFile`"))"
} else {
    Add-AfterAnchor `
        -Path $appHostProgram `
        -Anchor '^\s*var\s+\w+\s*=\s*sql\.AddDatabase\(' `
        -Insert @("var ${nameLower}Db = sql.AddDatabase(`"$appShortLower-$nameLower`", `"${appShort}_$Name`");") `
        -AlreadyApplied ([regex]::Escape("${nameLower}Db = sql.AddDatabase(")) `
        -Description "the orchestration host declares the $Name database" `
        -Manual "Add to the orchestration host's Program.cs, beside the existing AddDatabase call:`n    var ${nameLower}Db = sql.AddDatabase(`"$appShortLower-$nameLower`", `"${appShort}_$Name`");"

    # Chained onto the web project builder, so it is inserted WITHOUT a terminator: the statement it
    # joins ends further down the chain. Above the existing call rather than below it, for the reason
    # Add-BeforeAnchor exists: the last data-source call in the chain wins the Default source.
    Add-BeforeAnchor `
        -Path $appHostProgram `
        -Anchor '\.WithSQLServerDataSource\(' `
        -Insert @(".WithSQLServerDataSource(${nameLower}Db, `"$Name`")") `
        -AlreadyApplied ([regex]::Escape("WithSQLServerDataSource(${nameLower}Db")) `
        -Description "the orchestration host routes the $Name data source to the web host" `
        -Manual "Chain onto the web project in the orchestration host's Program.cs, ABOVE the existing call (the last one wins the Default source, and that has to stay the first module):`n    .WithSQLServerDataSource(${nameLower}Db, `"$Name`")"
}

# ---- 10. web host configuration ------------------------------------------------------------------
Write-Step 'Web host appsettings.json'

# The first-run normalization. A single-module solution needs no DataSources section at all: one
# module means one database and the top-level connection string is it. The second module is what
# makes routing real, so this section adds an entry for EVERY module, the one that was already here
# included, and pins the outbox explicitly.
$settings = Get-TextDocument $webSettings

$modulesRange = Get-JsonObjectRange -Document $settings -Key 'Modules'
$moduleIndent = (Get-IndentOf -Document $settings -Line $modulesRange.Open) + '  '

if (($settings.Lines -join "`n") -match ('"' + [regex]::Escape($Name) + '"\s*:\s*\{\s*"Enabled"')) {
    Write-Skipped "appsettings.json already enables $Name"
} else {
    Add-JsonMember -Document $settings -Range $modulesRange -Member @("$moduleIndent`"$Name`": { `"Enabled`": true }")
    Write-Applied "appsettings.json enables $Name under Modules"
}

# The top-level connection string stays (it is the Default fallback that startup validation and the
# health checks use) but its migrations-assembly pin must GO. Under Aspire every data-source call
# also rewrites the top-level connection string and the last one wins, so one module always collapses
# onto the Default source; a top-level pin naming the OTHER module's assembly then fails startup with
# a conflicting-value error. That is true of both engines: WithSqliteDataSource writes
# ConnectionStrings__SqliteConnectionString for exactly the same reason its SQL Server counterpart does.
$connectionRange = Get-JsonObjectRange -Document $settings -Key 'ConnectionStrings'
$connectionLine = $null
$pinLine = -1
for ($i = $connectionRange.Open + 1; $i -lt $connectionRange.Close; $i++) {
    if ($settings.Lines[$i] -match ('"' + $migrationsAssemblyKey + '"')) { $pinLine = $i }
    if ($settings.Lines[$i] -match ('"' + $connectionKey + '"\s*:\s*"([^"]*)"')) {
        $connectionLine = $Matches[1]
    }
}

if (-not $connectionLine) {
    throw "appsettings.json has no top-level $connectionKey, which is the $engineChoice connection this solution was detected as running on. Add the DataSources section by hand (dotnet new mmca-module prints the shape)."
}

if ($pinLine -lt 0) {
    Write-Skipped "appsettings.json has no top-level $migrationsAssemblyKey pin"
} else {
    $settings.Lines.RemoveAt($pinLine)
    # Whatever is now last in ConnectionStrings must lose the comma it carried as a non-last member.
    $last = $connectionRange.Close - 2
    while ($last -gt $connectionRange.Open -and -not $settings.Lines[$last].Trim()) { $last-- }
    $settings.Lines[$last] = $settings.Lines[$last].TrimEnd().TrimEnd(',')
    Write-Applied "appsettings.json drops the top-level $migrationsAssemblyKey pin"
}

# Each module's connection string is the existing one with only its database NAMED differently, so an
# adopter who already pointed the default at a real server (or at a directory of their own) keeps
# every other part of it for every module. Which part names the database is the engine's one real
# difference here: a server connection carries Database=, a file connection carries Data Source=.
function New-DataSourceLines {
    param([string] $Module, [string] $Indent)

    $connection = if ($engineChoice -eq 'sqlite') {
        [regex]::Replace($connectionLine, '(?i)(Data Source=)[^;]*', "`${1}${appShortLower}_$($Module.ToLowerInvariant()).db")
    } else {
        [regex]::Replace($connectionLine, '(?i)(Database=)[^;]*', "`${1}${appShort}_$Module")
    }

    return @(
        "$Indent`"$Module`": {"
        "$Indent  `"$connectionKey`": `"$connection`","
        "$Indent  `"$migrationsAssemblyKey`": `"$app.Migrations.$engineName.$Module`""
        "$Indent}"
    )
}

$rootRange = Get-RootObjectRange -Document $settings
$rootIndent = (Get-IndentOf -Document $settings -Line ($rootRange.Open + 1))
$dataSources = Get-JsonObjectRange -Document $settings -Key 'DataSources' -Optional

if ($null -eq $dataSources) {
    $entries = [System.Collections.Generic.List[string]]::new()
    $entries.Add("$rootIndent`"DataSources`": {")
    $allModules = @($existingModules) + @($Name)
    for ($m = 0; $m -lt $allModules.Count; $m++) {
        $lines = New-DataSourceLines -Module $allModules[$m] -Indent ($rootIndent + '  ')
        if ($m -lt $allModules.Count - 1) { $lines[-1] = $lines[-1] + ',' }
        $entries.AddRange([string[]] $lines)
    }
    $entries.Add("$rootIndent}")

    Add-JsonMember -Document $settings -Range (Get-RootObjectRange -Document $settings) -Member $entries.ToArray()
    Write-Applied "appsettings.json routes $($allModules.Count) module(s) through DataSources"
} elseif (($settings.Lines[$dataSources.Open..$dataSources.Close] -join "`n") -match ('"' + [regex]::Escape($Name) + '"\s*:')) {
    Write-Skipped "appsettings.json already has a DataSources entry for $Name"
} else {
    Add-JsonMember -Document $settings -Range $dataSources `
        -Member (New-DataSourceLines -Module $Name -Indent ((Get-IndentOf -Document $settings -Line $dataSources.Open) + '  '))
    Write-Applied "appsettings.json adds a DataSources entry for $Name"
}

# IEventBus writes handler-published integration events to ONE configured outbox source per host. It
# defaults to Default, and Default is whichever module's data-source call ran last (both engines
# rewrite the top-level connection string), so leaving it implicit means the outbox silently moves
# the day those calls are reordered.
if ($null -ne (Get-JsonObjectRange -Document $settings -Key 'Outbox' -Optional)) {
    Write-Skipped 'appsettings.json already pins the outbox source'
} else {
    Add-JsonMember -Document $settings -Range (Get-RootObjectRange -Document $settings) -Member @(
        "$rootIndent`"Outbox`": {"
        "$rootIndent  `"DatabaseName`": `"$firstModule`""
        "$rootIndent}"
    )
    Write-Applied "appsettings.json pins the outbox to the $firstModule database"
}

Save-TextDocument $settings

# ---- 11. first migration ------------------------------------------------------------------------
Write-Step 'First migration'

$migrationCommand = "dotnet ef migrations add InitialCreate --project $migrationsProject --startup-project $migrationsProject --context $dbContextName"

# On SQL Server the first migration can wait: the host creates and migrates the database at startup
# whatever the migrations assembly holds. On SQLite it cannot. A SQLite data source that names a
# migrations assembly is MIGRATED at startup rather than created outright, and an empty migrations
# assembly migrates nothing, so the host comes up against a database with no tables in it and the
# first query is what reports the problem. Said out loud wherever this step does not actually run.
$sqliteMigrationIsRequired = @"
This is a SQLite solution, so the migration above is required BEFORE the next run of the API host,
not something to get to later: the host migrates a SQLite source that names a migrations assembly
instead of creating it outright, and an empty migrations assembly leaves the database empty.
"@

$existingMigrations = @(Get-ChildItem -Path (Join-Path $root "$migrationsProject/Migrations") -File -Filter '*.cs' -ErrorAction SilentlyContinue)

if ($existingMigrations.Count -gt 0) {
    Write-Skipped "$migrationsProject already has $($existingMigrations.Count) migration file(s)"
} elseif ($SkipMigration) {
    Write-Host "  -SkipMigration: create it yourself with" -ForegroundColor Yellow
    Write-Host "    $migrationCommand"
    if ($engineChoice -eq 'sqlite') { Write-Warning $sqliteMigrationIsRequired }
} else {
    # The design-time factory opens no connection for `migrations add`, so this needs no database.
    # It DOES need the dotnet-ef tool, which is not part of the SDK. Missing it is not a reason to
    # fail a run whose other ten steps landed: print the command and let the adopter install it.
    & dotnet ef --version 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "The dotnet-ef tool is not installed, so the first migration was not created. Install it and run the command below:"
        Write-Host "    dotnet tool install --global dotnet-ef"
        Write-Host "    $migrationCommand"
        if ($engineChoice -eq 'sqlite') { Write-Warning $sqliteMigrationIsRequired }
    } else {
        # Restore first. The eight projects added above have never been restored, and dotnet ef does
        # not restore: it reads the project's MSBuild metadata, which without an assets file fails
        # with NETSDK1004 and the unhelpful "Unable to retrieve project metadata".
        Write-Host "  restoring the solution (the new projects have no assets file yet)"
        Invoke-Native 'dotnet restore' { dotnet restore $solution.FullName | Out-Null }

        Invoke-Native 'dotnet ef migrations add InitialCreate' {
            dotnet ef migrations add InitialCreate --project $migrationsProject --startup-project $migrationsProject --context $dbContextName
        }
        Write-Applied "created the InitialCreate migration in $migrationsProject"
    }
}

# ---- 12. summary --------------------------------------------------------------------------------
Write-Host ""
Write-Host "=== $Name is wired in ===" -ForegroundColor Green

foreach ($item in $script:Applied) { Write-Host "  + $item" }
foreach ($item in $script:Skipped) { Write-Host "  = $item (already done)" -ForegroundColor DarkGray }

Write-Host ""
Write-Host "Next:"
Write-Host "  dotnet build $($solution.Name)"
Write-Host "  dotnet test --solution $($solution.Name)"
Write-Host "  git diff        # the existing files edited above; this is the review"
if ($engineChoice -eq 'sqlite') {
    Write-Host "  the module's database is its own file, $moduleDbFile, created by the migration above"
    Write-Host "  the next run of the API host applies it; every later migration applies the same way"
}
Write-Host ""
Write-Host "Two things this deliberately did NOT do:"
Write-Host "  1. UI pages. The scaffold's Blazor host still shows only the first module. Copy a page"
Write-Host "     from it and point it at the new module's endpoints when you want one."
Write-Host "  2. Localization resources for those pages, for the same reason."
