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
#
# The two cases are the two SHAPES, not two names, and there are deliberately only two: each one is a
# full restore + Release build + test run, so the wall-clock cost of a third would buy less than
# widening these. Contoso.Support is the "catalog product" shape, every axis the template can take
# away taken away (no children, no lifecycle state, no owning user) plus the vocabulary rename that
# has no flag of its own to hide behind (--title). Zeta.Warehouse stays fully default, which is what
# makes the absence sweeps below non-vacuous: every presence branch and every default token has to
# survive there. Running both is the only way a marker region that removes one line too many (or too
# few) fails here rather than in an adopter's first build.
#
# --no-description and --event-verb are exercised on the MODULE added further down rather than here:
# they are the same two mechanisms (a whole-axis removal and a free-text rename), mmca-module
# declares the identical symbols, and proving them there costs one template instantiation instead of
# a third app case.
$cases = @(
    @{
        Name = 'Contoso.Support'; Module = 'Billing'; Aggregate = 'Invoice'
        Flat = $true; NoStatus = $true; NoOwner = $true; NoDescription = $false
        Title = 'Name'; EventVerb = ''
    },
    @{
        Name = 'Zeta.Warehouse'; Module = 'Reservations'; Aggregate = 'Reservation'
        Flat = $false; NoStatus = $false; NoOwner = $false; NoDescription = $false
        Title = ''; EventVerb = ''
    }
)

# Files exempt from the SHAPE and RENAME sweeps below, by leaf name. All four are copyOnly in
# .template.config/template.json, so no symbol replacement runs over them and each one legitimately
# carries a word the flags rename: the .editorconfig's analyzer descriptions use "comment" as an
# English word, PageHeading.razor holds the browser-tab element whose tag name ends in the --title
# word, SectionHeading.razor holds the MudBlazor typography member spelled "sub" plus that same word,
# and add-module.ps1 passes --title / --event-verb / --child through to dotnet new by name. Every one
# of them is still swept for the SEED's own tokens (helpdesk / ticket) above, and stage.ps1 guards
# their contents on their own terms; only the shape and vocabulary tokens are exempt here.
$copyOnlyLeaves = @('.editorconfig', 'PageHeading.razor', 'SectionHeading.razor', 'add-module.ps1')

# Both the SHAPE sweeps (a flag that removes an axis) and the RENAME sweeps (a flag that renames a
# vocabulary word) reduce to the same question: is the token gone from the generated tree. Three
# exemptions beyond the copyOnly leaves above, all deliberate. The generated README documents the
# flags themselves, so it names every axis by design; the UI page .resx files keep their full key set
# on purpose, because an orphan localization key costs nothing and conditionalizing six resource
# files to save it would not; and both are already swept for the SEED's own tokens elsewhere.
#
# Patterns stay deliberately narrow and case-sensitive. A blanket "Status" would fire on
# StatusCodes.Status200OK in every controller, which is ASP.NET's and not the aggregate's; the same
# discipline is why the owner axis is swept as "Requester" and not as "User", and why the two
# vocabulary words are swept in both their Pascal and camel spellings rather than case-insensitively
# (the template replaces "Title" and "title" as two separate symbols, and a rename that applies one
# but not the other is exactly the half-applied failure these sweeps exist to catch).
function Find-TokenResidue {
    param([string[]] $Roots, [string[]] $Patterns, [string] $Base)

    $regex = ($Patterns | ForEach-Object { [regex]::Escape($_) }) -join '|'

    foreach ($root in $Roots) {
        if (-not (Test-Path $root)) {
            throw "Token sweep was pointed at $root, which does not exist, so it would have passed having read nothing."
        }

        Get-ChildItem -Path $root -Recurse -File -Force |
            Where-Object {
                $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and
                $copyOnlyLeaves -notcontains $_.Name -and
                $_.Extension -ne '.resx' -and
                $_.Extension -ne '.md'
            } |
            ForEach-Object {
                $hit = Select-String -Path $_.FullName -Pattern $regex -CaseSensitive -List
                if ($hit) { "$($_.FullName.Substring($Base.Length)) line $($hit.LineNumber): $($hit.Line.Trim())" }
            }
    }
}

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

    $shapeFlags = @()
    if ($case.Flat) { $shapeFlags += '--flat' }
    if ($case.NoStatus) { $shapeFlags += '--no-status' }
    if ($case.NoOwner) { $shapeFlags += '--no-owner' }
    if ($case.NoDescription) { $shapeFlags += '--no-description' }
    if ($case.Title) { $shapeFlags += @('--title', $case.Title) }
    if ($case.EventVerb) { $shapeFlags += @('--event-verb', $case.EventVerb) }
    $shape = if ($shapeFlags) { $shapeFlags -join ' ' } else { 'default shape' }

    # Generate straight into WorkPath: preferNameDirectory already creates the <AppName> folder, so
    # a per-case subdirectory would nest it twice and cost 20 more characters of path budget.
    Invoke-Step "Generate $appName (module $($case.Module), aggregate $($case.Aggregate), $shape)" {
        Push-Location $WorkPath
        try {
            dotnet new mmca-app -n $appName --module $case.Module --aggregate $case.Aggregate @shapeFlags --no-restore
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

    # The shape flags remove code rather than disable it, and the vocabulary flags rename it rather
    # than alias it, so in both cases the proof is that the token is GONE, not that the app happens
    # to build: a marker region that removed one line too few, and a rename that only half applied,
    # both compile more often than not.
    if ($shapeFlags) {
        Invoke-Step "Shape sweep $appName ($shape)" {
            $shapePatterns = @()
            if ($case.Flat) { $shapePatterns += 'Comment' }
            if ($case.NoStatus) { $shapePatterns += "$($case.Aggregate)Status", 'ChangeStatus' }
            if ($case.NoOwner) { $shapePatterns += 'Requester', 'requester' }
            if ($case.NoDescription) { $shapePatterns += 'Description', 'description' }
            if ($case.Title) { $shapePatterns += 'Title', 'title' }
            if ($case.EventVerb) { $shapePatterns += 'Opened', 'opened' }

            $offenders = @(Find-TokenResidue -Roots @($appRoot) -Patterns $shapePatterns -Base $appRoot)

            if ($offenders) {
                throw @"
Generated with $shape but $($offenders.Count) file(s) still carry a removed axis or an un-renamed word:
  $($offenders -join "`n  ")
"@
            }
            Write-Host "  no '$($shapePatterns -join "' / '")' tokens survive $shape"
        }
    }

    # The mirror of the sweep above, and the whole reason it is not vacuous. An absence check passes
    # just as happily when an axis was DELETED as when it was correctly conditionalized, and it
    # passes at its very happiest when the axis never reached the template at all: a symbol dropped
    # from template.json, a marker region that swallowed the property it was meant to guard, or a
    # sources.modifiers path typo that excludes a file unconditionally would all read as "clean".
    # So every token this case did NOT ask to lose is asserted PRESENT, in the same tree and under
    # the same file filter, which is what makes the default case carry its weight.
    Invoke-Step "Retained-token presence $appName" {
        $expected = @()
        if (-not $case.Flat) { $expected += 'Comment' }
        if (-not $case.NoStatus) { $expected += "$($case.Aggregate)Status" }
        if (-not $case.NoOwner) { $expected += 'RequesterUserId' }
        if (-not $case.NoDescription) { $expected += 'Description' }
        if (-not $case.Title) { $expected += 'Title' }
        if (-not $case.EventVerb) { $expected += 'Opened' }

        $missing = @($expected | Where-Object {
            -not @(Find-TokenResidue -Roots @($appRoot) -Patterns @($_) -Base $appRoot)
        })

        if ($missing) {
            throw @"
$($missing.Count) token(s) that $appName never asked to drop are absent from its generated tree:
  $($missing -join "`n  ")
A rename that half-applies and an axis that was deleted rather than made conditional both look
identical to the shape sweep above. This is the check that tells them apart.
"@
        }
        Write-Host "  $($expected.Count) retained token(s) still present: $($expected -join ', ')"
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

    # The FALSE branch of the eager-load conditional, proved against an aggregate that genuinely owns
    # no collection. That is the whole point of the conditional, and --flat is the shape that makes
    # this app's aggregate that way, so a slice naming a navigation here does not merely look wrong,
    # it does not compile. The TRUE branch is proved further down, inside the module added below,
    # whose aggregate does own children.
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

    # Asserted on the generated text rather than only on the build. Without --child-collection the
    # eager-load must be EMPTY, not renamed.
    Invoke-Step "Eager-load conditional $appName" {
        $bare = @(
            Join-Path $useCases "Archive$($case.Aggregate)/Archive$($case.Aggregate)Handler.cs"
            Join-Path $useCases "Get$($case.Aggregate)ByNumber/Get$($case.Aggregate)ByNumberHandler.cs"
        )

        foreach ($handler in $bare) {
            if (-not (Test-Path $handler)) { throw "No generated handler at $handler" }
            $text = Get-Content $handler -Raw
            if ($text -match '//#if|//#else|//#endif') {
                throw "Conditional directives survived into $handler. The templating engine did not process them, so the file ships disabled scaffolding."
            }
            if ($text -match 'includes: \[nameof') {
                throw "Generated without --child-collection but $handler still names a navigation. It must fall back to an empty include list."
            }
            # Not merely absent: GetByIdAsync declares includes as a REQUIRED parameter, so omitting
            # the argument trades the missing-navigation error for CS7036.
            if ($text -notmatch 'includes: \[\],') {
                throw "Generated without --child-collection but $handler passes no includes argument at all. GetByIdAsync requires one."
            }
        }

        Write-Host "  empty include list without --child-collection (command and query)"
    }

    Invoke-Step "Token sweep $appName slices" {
        $sliceFiles = @(Get-ChildItem -Path $useCases -Recurse -File |
            Where-Object { $_.DirectoryName -match 'Archive|ByNumber' })
        # 3 for the command slice (command, its validator, handler) and 2 for the query slice.
        if ($sliceFiles.Count -ne 5) { throw "Expected 5 slice files, found $($sliceFiles.Count)" }

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

    # ---- mmca-module, added the way an adopter adds one ----------------------------------------
    # Through the generated app's OWN build/add-module.ps1, not through dotnet new plus a wire-up
    # this script hand-rolls. That is the point of the step: mmca-module's manualInstructions are
    # the template's most fragile deliverable, and the script is what performs them, so exercising
    # the script IS the coverage. A step missing from it (project references were, at first) shows
    # up as a compile error nowhere else.
    $newModule = 'Shipping'
    $newAggregate = 'Shipment'
    $newChild = 'Item'
    # The two axes no app case above carries, put on the module so they cost one template
    # instantiation instead of a third full restore-build-test app. --no-description is the second
    # whole-axis removal (the first, --no-owner, is on the app case) and --event-verb is the second
    # free-text rename (the first, --title, is likewise). --no-owner and --title ride along too,
    # deliberately: mmca-app and mmca-module declare those symbols SEPARATELY, in two template.json
    # files, so proving them in one says nothing about the other.
    $newTitle = 'Subject'
    $newEventVerb = 'Dispatched'
    $short = $appName.Split('.')[-1]
    $shortLower = $short.ToLowerInvariant()
    $newModuleLower = $newModule.ToLowerInvariant()

    $addModule = Join-Path $appRoot 'build/add-module.ps1'
    if (-not (Test-Path $addModule)) {
        throw "The generated app ships no build/add-module.ps1. The overlay file did not reach the package; check build/templates/stage.ps1 and the Content glob in MMCA.Templates.csproj."
    }

    # dotnet-ef is a global tool, not part of the SDK, so it is present on a dev box and usually
    # absent on a CI runner. The script degrades to printing the command when it is missing; the
    # assertion below follows the same branch rather than pretending the migration must exist.
    dotnet ef --version 2>&1 | Out-Null
    $hasDotnetEf = $LASTEXITCODE -eq 0
    $global:LASTEXITCODE = 0
    Write-Host "dotnet-ef $(if ($hasDotnetEf) { 'present: the first migration will be created' } else { 'absent: the migration step will print its command instead' })"

    # --child Item, deliberately, and into an app generated --flat: one solution then holds a module
    # with no children beside a module whose child is named something the seed never used. The child
    # rename is a three-way substitution (type, plural navigation, lower-case route), and the only
    # thing that catches a half-applied one is asserting on the generated names.
    Invoke-Step "Add module $newModule to $appName via build/add-module.ps1 (child $newChild, no owner, no description, title $newTitle, event verb $newEventVerb)" {
        Push-Location $appRoot
        try {
            # In a child pwsh so the run proves what an adopter actually types, exit code included:
            # dot-sourcing it would share this script's own preferences and hide a failure mode.
            # Every shape and vocabulary switch the script declares is passed here except -Flat and
            # -NoStatus, which this module deliberately keeps (its aggregate is the one in the run
            # that owns a collection, and the TRUE branch of the eager-load conditional below needs
            # it), and -SkipMigration, whose absence is what makes the migration step real.
            pwsh -NoProfile -File $addModule `
                -Name $newModule -Aggregate $newAggregate -Child $newChild `
                -NoOwner -NoDescription -Title $newTitle -EventVerb $newEventVerb
            if ($LASTEXITCODE -ne 0) { throw "build/add-module.ps1 failed with exit code $LASTEXITCODE" }
        } finally {
            Pop-Location
        }
    }

    $newModuleRoot = Join-Path $appRoot "Source/Modules/$newModule"

    Invoke-Step "Child rename $newAggregate$newChild" {
        $expectedFiles = @(
            "$appName.$newModule.Domain/$newModule/$newAggregate$newChild.cs"
            "$appName.$newModule.Shared/$newModule/$newAggregate${newChild}DTO.cs"
            "$appName.$newModule.Shared/$newModule/Add${newChild}Request.cs"
            "$appName.$newModule.Shared/$newModule/Edit${newChild}Request.cs"
            "$appName.$newModule.Application/$newModule/UseCases/Add$newChild/Add${newChild}Handler.cs"
            "$appName.$newModule.Application/$newModule/UseCases/Edit$newChild/Edit${newChild}Handler.cs"
            "$appName.$newModule.Application/$newModule/UseCases/Remove$newChild/Remove${newChild}Handler.cs"
        )
        foreach ($relative in $expectedFiles) {
            $full = Join-Path $newModuleRoot $relative
            if (-not (Test-Path $full)) { throw "--child $newChild produced no $relative" }
        }

        $entity = Get-Content (Join-Path $newModuleRoot "$appName.$newModule.Domain/$newModule/$newAggregate$newChild.cs") -Raw
        if ($entity -notmatch "public sealed class $newAggregate$newChild\b") {
            throw "The generated child entity is not named $newAggregate$newChild."
        }

        $controller = Get-Content (Join-Path $newModuleRoot "$appName.$newModule.API/Controllers/${newModule}Controller.cs") -Raw
        if ($controller -notmatch [regex]::Escape('"{id}/items"')) {
            throw "The generated controller has no /items route, so --child did not reach the lower-case plural form."
        }
        if ($controller -match 'Comment') {
            throw "The generated controller still names Comment, so --child only half applied."
        }

        Write-Host "  $newAggregate$newChild, Add/Edit/Remove$newChild slices, /items routes"
    }

    # mmca-module's own copy of the shape and rename sweeps. It declares --no-owner, --no-description,
    # --title and --event-verb in its OWN template.json, with its own marker regions converted by its
    # own staging pass, so nothing the app cases above prove carries over. Scoped to the three trees
    # the template writes rather than to the whole solution, because the first module still has every
    # axis this one dropped.
    Invoke-Step "Shape sweep module $newModule (no owner, no description, title $newTitle, event verb $newEventVerb)" {
        $moduleTrees = @(
            Join-Path $appRoot "Source/Modules/$newModule"
            Join-Path $appRoot "Tests/Modules/$newModule"
            Join-Path $appRoot "Source/Hosting/$appName.Migrations.SqlServer.$newModule"
        )
        $modulePatterns = @('Requester', 'requester', 'Description', 'description', 'Title', 'title', 'Opened', 'opened')

        $offenders = @(Find-TokenResidue -Roots $moduleTrees -Patterns $modulePatterns -Base $appRoot)
        if ($offenders) {
            throw @"
$newModule was generated with --no-owner --no-description --title $newTitle --event-verb $newEventVerb
but $($offenders.Count) file(s) still carry a removed axis or an un-renamed word:
  $($offenders -join "`n  ")
"@
        }

        # And the other half, for the same reason the app cases assert their retained tokens: a rename
        # that produced NOTHING sweeps just as clean as one that produced the right thing. Asserted on
        # the two names that only exist if both free-text symbols landed, one of which is a fileRename.
        $renamed = @(
            Join-Path $appRoot "Source/Modules/$newModule/$appName.$newModule.Shared/$newModule/IntegrationEvents/$newAggregate${newEventVerb}IntegrationEvent.cs"
            Join-Path $appRoot "Source/Modules/$newModule/$appName.$newModule.Application/$newModule/IntegrationEventHandlers/$newAggregate${newEventVerb}Handler.cs"
        )
        foreach ($file in $renamed) {
            if (-not (Test-Path $file)) {
                throw "--event-verb $newEventVerb produced no $(Split-Path $file -Leaf). The symbol's fileRename no longer matches the seed's file names."
            }
        }

        $aggregate = Join-Path $appRoot "Source/Modules/$newModule/$appName.$newModule.Domain/$newModule/$newAggregate.cs"
        if ((Get-Content $aggregate -Raw) -notmatch "public string $newTitle\b") {
            throw "--title $newTitle did not reach the aggregate's main text property in $aggregate."
        }

        Write-Host "  no owner / description / Title / Opened tokens, and $newAggregate$newEventVerb* + $newTitle landed"
    }

    # Every edit the script claims to make, asserted on the FILES rather than on its own output. A
    # wire-up that silently did nothing still lets the solution build (the module is simply invisible
    # to the host and to the fitness rules), which is exactly the failure the printed instructions
    # existed to prevent and exactly what a build-only check would miss.
    Invoke-Step "Wire-up state after add-module.ps1" {
        function Assert-Match {
            param([string] $Relative, [string] $Pattern, [string] $What, [int] $Expected = 1)

            $full = Join-Path $appRoot $Relative
            if (-not (Test-Path $full)) { throw "$What : no $Relative in the generated app." }
            $hits = ([regex]::Matches((Get-Content $full -Raw), $Pattern)).Count
            if ($hits -ne $Expected) {
                throw "$What : expected $Expected match(es) of /$Pattern/ in $Relative, found $hits."
            }
        }

        # 1. solution: the eight new projects, no more and no fewer.
        Assert-Match "$appName.slnx" ('<Project Path="[^"]*' + $newModule + '[^"]*"') 'solution entries' 8

        # 2. web host: the module API (controllers and error resources) and its migrations assembly.
        $webCsproj = "Source/Hosts/$appName.Web/$appName.Web.csproj"
        Assert-Match $webCsproj ([regex]::Escape("$appName.$newModule.API.csproj")) 'web host API reference'
        Assert-Match $webCsproj ([regex]::Escape("$appName.Migrations.SqlServer.$newModule.csproj")) 'web host migrations reference'

        # 3. architecture tests: all five layers, because the map names a type from each.
        $archCsproj = "Tests/Architecture/$appName.Architecture.Tests/$appName.Architecture.Tests.csproj"
        foreach ($layer in @('Domain', 'Application', 'Infrastructure', 'Shared', 'API')) {
            Assert-Match $archCsproj ([regex]::Escape("$appName.$newModule.$layer.csproj")) "architecture-test $layer reference"
        }

        # 4. identifier alias, linked into every project but its own.
        $alias = "$appName.$newModule.GlobalUsings.IdentifierType.cs"
        Assert-Match 'Directory.Build.props' ([regex]::Escape($alias)) 'alias Compile + Link' 2
        Assert-Match 'Directory.Build.props' ([regex]::Escape("!= '$appName.$newModule.Shared'")) 'alias self-exclusion condition'

        # 5. architecture map: five lines, one per layer. A module missing from the map is not a
        # failing test, it is a silently unchecked one.
        $map = "Tests/Architecture/$appName.Architecture.Tests/${short}ArchitectureMap.cs"
        Assert-Match $map ([regex]::Escape("Module(`"$newModule`",")) 'architecture map lines' 5
        Assert-Match $map ([regex]::Escape("typeof($appName.$newModule.API.Controllers.${newModule}Controller)")) 'architecture map API type'

        # 6. host error resources: the one registration ModuleLoader does not do for you.
        $program = "Source/Hosts/$appName.Web/Program.cs"
        Assert-Match $program ([regex]::Escape("using $appName.$newModule.API.Resources;")) 'error-resources using'
        Assert-Match $program ([regex]::Escape("services.AddErrorResources<${newModule}ErrorResources>();")) 'AddErrorResources call'

        # 7. AppHost: the module's own database plus its data-source routing. One database per module
        # because each carries its own outbox and inbox tables.
        $appHost = "Source/Hosting/$appName.AppHost/Program.cs"
        Assert-Match $appHost ([regex]::Escape("var ${newModuleLower}Db = sql.AddDatabase(`"$shortLower-$newModuleLower`", `"${short}_$newModule`");")) 'AppHost database resource'
        Assert-Match $appHost ([regex]::Escape(".WithSQLServerDataSource(${newModuleLower}Db, `"$newModule`")")) 'AppHost data-source routing'

        Write-Host "  solution, both csproj files, the alias link, the map, Program.cs and the AppHost"
    }

    # appsettings.json gets its own step because the script does not merely ADD to it: this is the
    # first-run normalization, where a single-module scaffold (no DataSources section at all, a
    # migrations-assembly pin at top level) becomes a multi-module one. Parsing the result is half
    # the assertion: the edits are anchored text, so valid JSON coming out is not free.
    Invoke-Step "appsettings.json normalized for two modules" {
        $appSettingsPath = Join-Path $appRoot "Source/Hosts/$appName.Web/appsettings.json"
        $settings = Get-Content $appSettingsPath -Raw | ConvertFrom-Json

        if (-not $settings.Modules.PSObject.Properties[$newModule]) {
            throw "Modules has no $newModule entry."
        }
        if (-not $settings.Modules.$newModule.Enabled) {
            throw "Modules.$newModule is present but not enabled."
        }

        # The top-level connection string STAYS (it is the Default fallback used by startup
        # validation and the health checks); its assembly pin must not, because under Aspire the last
        # WithSQLServerDataSource call wins the top-level connection string, so one module always
        # collapses onto Default and a pin naming the other module's assembly fails startup.
        if (-not $settings.ConnectionStrings.PSObject.Properties['SQLServerConnectionString']) {
            throw "The top-level SQLServerConnectionString was removed. Only its assembly pin should have been."
        }
        if ($settings.ConnectionStrings.PSObject.Properties['SQLServerMigrationsAssembly']) {
            throw "The top-level SQLServerMigrationsAssembly pin survived. Startup would fail with a conflicting-value error once both modules route their own data source."
        }

        if (-not $settings.PSObject.Properties['DataSources']) {
            throw "No DataSources section. A single-module scaffold has none, and creating it for EVERY module is the normalization this step exists to prove."
        }
        foreach ($module in @($case.Module, $newModule)) {
            $entry = $settings.DataSources.PSObject.Properties[$module]
            if (-not $entry) { throw "DataSources has no $module entry." }
            if ($entry.Value.SQLServerMigrationsAssembly -ne "$appName.Migrations.SqlServer.$module") {
                throw "DataSources.$module pins '$($entry.Value.SQLServerMigrationsAssembly)', expected $appName.Migrations.SqlServer.$module."
            }
            if ($entry.Value.SQLServerConnectionString -notmatch [regex]::Escape("Database=${short}_$module")) {
                throw "DataSources.$module does not target the ${short}_$module database: $($entry.Value.SQLServerConnectionString)"
            }
        }

        # IEventBus writes handler-published integration events to ONE outbox source per host, and it
        # defaults to whichever module's data source ran last. Naming it is what stops the outbox
        # moving to another module's database the day those calls are reordered.
        if (-not $settings.PSObject.Properties['Outbox']) { throw "No top-level Outbox section." }
        if ($settings.Outbox.DatabaseName -ne $case.Module) {
            throw "Outbox is pinned to '$($settings.Outbox.DatabaseName)', expected the first module $($case.Module)."
        }

        Write-Host "  $newModule enabled, 2 DataSources entries, outbox pinned to $($case.Module), top-level assembly pin gone"
    }

    Invoke-Step "First migration for $newModule" {
        $migrations = Join-Path $appRoot "Source/Hosting/$appName.Migrations.SqlServer.$newModule/Migrations"
        $created = @(Get-ChildItem -Path $migrations -File -Filter '*.cs' -ErrorAction SilentlyContinue)

        if ($hasDotnetEf) {
            if ($created.Count -eq 0) {
                throw "dotnet-ef is installed but add-module.ps1 created no migration under $migrations."
            }
            if (-not ($created.Name -match 'InitialCreate')) {
                throw "Migrations exist under $migrations but none is named InitialCreate: $($created.Name -join ', ')"
            }
            Write-Host "  $($created.Count) file(s), InitialCreate present"
        } else {
            if ($created.Count -ne 0) {
                throw "dotnet-ef is absent yet $($created.Count) migration file(s) appeared under $migrations."
            }
            Write-Host "  skipped: dotnet-ef is not installed, and the script printed the command instead"
        }
    }

    # Rerunning with a name that is already here must fail at the preflight, before it generates or
    # patches anything. Half-applying a second copy of eight projects and six edits is the one
    # outcome worse than refusing.
    Invoke-Step "Rerunning add-module.ps1 for $newModule fails fast" {
        Push-Location $appRoot
        try {
            $before = (Get-Content (Join-Path $appRoot "Source/Hosts/$appName.Web/Program.cs") -Raw)
            $rerun = (pwsh -NoProfile -File $addModule -Name $newModule -Aggregate $newAggregate 2>&1 | Out-String)
            if ($LASTEXITCODE -eq 0) {
                throw "add-module.ps1 reran for an existing module and reported success."
            }
            if ($rerun -notmatch 'already exists') {
                throw "add-module.ps1 failed on the rerun, but not with the preflight's already-exists message:`n$rerun"
            }
            $after = (Get-Content (Join-Path $appRoot "Source/Hosts/$appName.Web/Program.cs") -Raw)
            if ($after -ne $before) {
                throw "The refused rerun still modified Program.cs. Preflight must run before any edit."
            }
        } finally {
            $global:LASTEXITCODE = 0
            Pop-Location
        }
        Write-Host "  refused at preflight, nothing written"
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

    # The TRUE branch of the eager-load conditional, and the one place in the run where an aggregate
    # actually owns a collection. It is also named Items rather than Comments, which is what proves
    # --child-collection is a free-text symbol and not a second spelling of the seed's navigation.
    $newUseCases = Join-Path $newModuleRoot "$appName.$newModule.Application/$newModule/UseCases"

    Invoke-Step "Generate --child-collection slices into $newModule" {
        Push-Location $newUseCases
        try {
            dotnet new mmca-command -n "Restore$newAggregate" --app $appName --module $newModule --aggregate $newAggregate --domain-method Delete --child-collection "${newChild}s"
            if ($LASTEXITCODE -ne 0) { throw "mmca-command --child-collection failed with exit code $LASTEXITCODE" }

            dotnet new mmca-query -n "Find${newAggregate}ByCode" --app $appName --module $newModule --aggregate $newAggregate --child-collection "${newChild}s"
            if ($LASTEXITCODE -ne 0) { throw "mmca-query --child-collection failed with exit code $LASTEXITCODE" }
        } finally {
            Pop-Location
        }

        $withChild = @(
            Join-Path $newUseCases "Restore$newAggregate/Restore${newAggregate}Handler.cs"
            Join-Path $newUseCases "Find${newAggregate}ByCode/Find${newAggregate}ByCodeHandler.cs"
        )
        foreach ($handler in $withChild) {
            if (-not (Test-Path $handler)) { throw "No generated handler at $handler" }
            $text = Get-Content $handler -Raw
            if ($text -match '//#if|//#else|//#endif') {
                throw "Conditional directives survived into $handler. The templating engine did not process them, so the file ships disabled scaffolding."
            }
            if ($text -notmatch "includes: \[nameof\($newAggregate\.${newChild}s\)\]") {
                throw "Generated with --child-collection ${newChild}s but $handler has no matching eager-load."
            }
        }

        Write-Host "  named navigation with --child-collection (command and query)"
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
