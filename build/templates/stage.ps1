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

# ---- template markers -> dotnet-new conditional directives --------------------------------------
# The seed carries the optional axes as ORDINARY COMMENTS:
#
#     // template:begin child          <!-- template:begin child -->     @* template:begin child *@
#     ...                              ...                              ...
#     // template:end child            <!-- template:end child -->       @* template:end child *@
#
# so the reference app still compiles warning-free under all five analyzers and its tests still run:
# the markers are comments and nothing else. They become conditional directives HERE, in the staged
# copies only, which is the same rule the eager-load injection below follows and the same reason:
# directives in the seed would be real syntax in a solution whose CI has to stay green.
#
# Whole FILES are not handled here. Those are listed in each template.json's sources.modifiers, where
# an excluded file needs no content surgery at all.
#
# Combination labels exist for the regions that only make sense while SEVERAL axes are present, and
# they come in both polarities: 'childStatus' and 'statusOwner' need BOTH of their axes, while
# 'statusOrOwner' needs EITHER (it wraps a UI block that would otherwise render empty once both of
# the values inside it are gone).
#
# The last four are the SOLUTION axes rather than the module's: --database sqlserver|sqlite and
# --no-aspire. They come in both polarities for the same reason the module axes do, and 'sqlite'
# exists even though NO seed file carries that marker: the seed cannot hold two engines' code at
# once, so the sqlite branches are INJECTED at staging time by Add-EngineAlternative below, which
# writes them as ordinary 'sqlite' marker regions for this same pass to convert.
$markerConditions = @{
    'child'           = '!flat'
    'status'          = '!noStatus'
    'childStatus'     = '!(flat || noStatus)'
    'description'     = '!noDescription'
    'owner'           = '!noOwner'
    'statusOwner'     = '!(noStatus || noOwner)'
    'statusOrOwner'   = '!(noStatus && noOwner)'
    'childOrOwner'    = '!(flat && noOwner)'
    'sqlserver'       = '!useSqlite'
    'sqlite'          = 'useSqlite'
    'aspire'          = '!noAspire'
    'aspireSqlServer' = '!(noAspire || useSqlite)'
}

# The two solution axes reach different templates, so the strip lists are not one list.
#
# The HOST axis (aspire / aspireSqlServer) is mmca-app's alone: only the app owns an orchestration
# project and the pins that go with it. mmca-module and the slices declare no noAspire symbol, and a
# condition on an undeclared symbol is silently false, so there those markers are STRIPPED (the
# content stays, the two marker lines go).
#
# The ENGINE axis is mmca-app's AND mmca-module's. A module's EF configurations inherit an
# engine-specific base and its migrations project references an engine-specific provider, so a
# SQL-Server-shaped module dropped into a sqlite app does not merely look wrong: it names a
# configuration base and a package that app does not have. mmca-module therefore declares --database
# with the same two derived symbols, and its 'sqlserver' / 'sqlite' markers are CONVERTED rather than
# stripped. The slices carry neither axis (one use case is engine-neutral) and drop every marker they
# meet through Remove-TemplateMarkers rather than through this list.
$moduleStripLabels = @('aspire', 'aspireSqlServer')

# dotnet new picks the conditional syntax from the file extension, and it is not one syntax. C files
# take line comments, XML/resx take comment elements, and .razor takes a razor-comment form whose
# else/endif are '##' rather than '//' (verified against the SDK's own BlazorWeb template). Emitting
# the wrong one is silent: the directives survive as text into the generated app.
$markerStyles = @{
    '.cs'      = @{ If = '//#if ({0})';      EndIf = '//#endif' }
    '.razor'   = @{ If = '@*#if ({0})';      EndIf = '##endif*@' }
    '.cshtml'  = @{ If = '@*#if ({0})';      EndIf = '##endif*@' }
    '.resx'    = @{ If = '<!--#if ({0})-->'; EndIf = '<!--#endif-->' }
    '.xml'     = @{ If = '<!--#if ({0})-->'; EndIf = '<!--#endif-->' }
    '.csproj'  = @{ If = '<!--#if ({0})-->'; EndIf = '<!--#endif-->' }
    '.props'   = @{ If = '<!--#if ({0})-->'; EndIf = '<!--#endif-->' }
    '.targets' = @{ If = '<!--#if ({0})-->'; EndIf = '<!--#endif-->' }
    '.config'  = @{ If = '<!--#if ({0})-->'; EndIf = '<!--#endif-->' }
    # .slnx is XML and the solution parsers keep comments, which is what lets --no-aspire drop the
    # AppHost's <Project> line the same way every other XML file drops a region. Verified by building
    # the seed's own .slnx with the marker comments in it.
    '.slnx'    = @{ If = '<!--#if ({0})-->'; EndIf = '<!--#endif-->' }
}

# One pattern, three comment shapes. .NET allows the same group name on alternate branches, so 'kind'
# and 'label' read back whichever branch matched. '//+' also picks up a marker written as '///', which
# is how a region inside an XML doc block is expressed without splitting the block in the seed.
$markerPattern = '^(?<indent>[ \t]*)(?://+\s*template:(?<kind>begin|end)\s+(?<label>\w+)|<!--\s*template:(?<kind>begin|end)\s+(?<label>\w+)\s*-->|@\*\s*template:(?<kind>begin|end)\s+(?<label>\w+)\s*\*@)[ \t]*$'

function Convert-TemplateMarkers {
    param(
        [string] $Root,
        [string] $TemplateName,
        [string[]] $StripLabels = @()
    )

    $perAxis = @{}
    $regions = 0
    $touched = 0
    $stripped = 0

    foreach ($file in Get-ChildItem -Path $Root -Recurse -File -Force) {
        $text = Get-Content $file.FullName -Raw
        if (-not $text -or $text -notmatch 'template:(begin|end)') { continue }

        $ext = $file.Extension.ToLowerInvariant()
        if (-not $markerStyles.ContainsKey($ext)) {
            throw "${TemplateName}: $($file.FullName) carries template markers but '$ext' has no conditional syntax mapped in stage.ps1. dotnet new would ship the marker text verbatim into the generated app. Add the extension to `$markerStyles or drop the markers."
        }
        $style = $markerStyles[$ext]
        $newline = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
        $lines = $text -split "`r?`n"
        $stack = [System.Collections.Generic.Stack[string]]::new()
        $dropped = [System.Collections.Generic.HashSet[int]]::new()

        for ($i = 0; $i -lt $lines.Count; $i++) {
            $match = [regex]::Match($lines[$i], $markerPattern)
            if (-not $match.Success) {
                if ($lines[$i] -match 'template:(begin|end)') {
                    throw "${TemplateName}: $($file.FullName) line $($i + 1) looks like a template marker but does not parse: '$($lines[$i].Trim())'. Expected exactly '<comment> template:begin|end <label>'."
                }
                continue
            }

            $label = $match.Groups['label'].Value
            if (-not $markerConditions.ContainsKey($label)) {
                throw "${TemplateName}: $($file.FullName) line $($i + 1) uses the unknown marker axis '$label'. Known axes: $($markerConditions.Keys -join ', ')."
            }

            # A stripped axis keeps its CONTENT and loses only the two marker lines, so the balance
            # check above still runs over it: an unbalanced region is a seed bug in every template,
            # not only in the ones that declare the symbol.
            $strip = $StripLabels -contains $label

            if ($match.Groups['kind'].Value -eq 'begin') {
                $stack.Push($label)
                if ($strip) {
                    [void] $dropped.Add($i)
                    $stripped++
                } else {
                    $lines[$i] = $match.Groups['indent'].Value + ($style.If -f $markerConditions[$label])
                    $regions++
                    if (-not $perAxis.ContainsKey($label)) { $perAxis[$label] = 0 }
                    $perAxis[$label]++
                }
            } else {
                if ($stack.Count -eq 0) {
                    throw "${TemplateName}: $($file.FullName) line $($i + 1) closes a '$label' region that was never opened."
                }
                $open = $stack.Pop()
                if ($open -ne $label) {
                    throw "${TemplateName}: $($file.FullName) line $($i + 1) closes '$label' while '$open' is still open. Marker regions must nest."
                }
                if ($strip) {
                    [void] $dropped.Add($i)
                    $stripped++
                } else {
                    $lines[$i] = $match.Groups['indent'].Value + $style.EndIf
                }
            }
        }

        if ($stack.Count -gt 0) {
            throw "${TemplateName}: $($file.FullName) ends with $($stack.Count) unclosed marker region(s): $($stack -join ', ')."
        }

        $kept = @(for ($i = 0; $i -lt $lines.Count; $i++) { if (-not $dropped.Contains($i)) { $lines[$i] } })
        Set-Content -Path $file.FullName -Value ($kept -join $newline) -NoNewline
        $touched++
    }

    $breakdown = ($perAxis.Keys | Sort-Object | ForEach-Object { "$_=$($perAxis[$_])" }) -join ', '
    Write-Host "$TemplateName marker regions converted: $regions across $touched file(s) ($breakdown)"
    if ($stripped -gt 0) {
        Write-Host "${TemplateName}: $stripped marker line(s) stripped for axes this template does not declare ($($StripLabels -join ', '))"
    }
}

# ---- the second branch of an either/or axis ------------------------------------------------------
# --database is the first axis that is not a REMOVAL. Every other one takes code away, which a marker
# region expresses directly; this one swaps one spelling for another, and the seed can only hold one
# of the two: it is a real solution whose CI has to build, and a commented-out second branch would
# both fail S125 (commented-out code) and rot unread.
#
# So the seed keeps the SQL Server branch as ordinary code inside a 'sqlserver' marker region, and the
# SQLite branch lives HERE, in the staging script, injected as a sibling 'sqlite' region right after
# the region it alternates with. Convert-TemplateMarkers then converts both in the same pass, and the
# generated app gets exactly one of them. dotnet new's symbol replacement runs over the injected lines
# like any other staged text, so they may (and do) name the seed's own tokens.
#
# The anchor is the region's END marker rather than a line of code: a body that gets reflowed does not
# move it, while removing or renaming the region does, and that is precisely when this table is wrong.
function Add-EngineAlternative {
    param(
        [string] $Path,
        [string] $Marker,
        [string[]] $Lines,
        [string] $TemplateName
    )

    $text = Get-Content $Path -Raw
    $newline = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $sourceLines = $text -split "`r?`n"

    $endPattern = "^[ \t]*(?://+|<!--)\s*template:end\s+$([regex]::Escape($Marker))\b"
    $anchors = @(0..($sourceLines.Count - 1) | Where-Object { $sourceLines[$_] -match $endPattern })

    if ($anchors.Count -ne 1) {
        throw "${TemplateName}: expected exactly one 'template:end $Marker' line in $Path, found $($anchors.Count). The region this engine alternative attaches to moved or was renamed; update `$engineAlternatives in stage.ps1 rather than shipping a template whose sqlite shape still emits SQL Server code."
    }

    $at = $anchors[0]
    $comment = if ($sourceLines[$at] -match '<!--') { '<!--{0}-->' } else { '// {0}' }
    $indent = [regex]::Match($sourceLines[$at], '^[ \t]*').Value

    $injected = @($indent + ($comment -f 'template:begin sqlite')) + $Lines + @($indent + ($comment -f 'template:end sqlite'))
    $out = @($sourceLines[0..$at]) + $injected + @($sourceLines[($at + 1)..($sourceLines.Count - 1)])

    Set-Content -Path $Path -Value ($out -join $newline) -NoNewline
}

# Scope is which templates stage the file, exactly as in $optionalAxisLines below. The AppHost block
# is a host file only mmca-app carries; the design-time factory appears under both scopes, because
# mmca-module stages the migrations project too and a generated module has to be able to land in a
# sqlite app.
$engineAlternatives = @(
    # The same file, staged by both templates, with a different default file name in each. mmca-app
    # generates the FIRST module, whose database file is the one the app's own appsettings names
    # (<app-short>.db); mmca-module generates a LATER one, and every module owns its own database, so
    # its file has to be the per-module one build/add-module.ps1 writes into DataSources
    # (<app-short>_<module>.db). One shared entry would hand every added module the first module's
    # file and collide their outbox tables on the first migration.
    @{
        Scope = 'app'
        Path = 'Source/Hosting/MMCA.Helpdesk.Migrations.SqlServer.Tickets/DesignTimeSQLServerDbContextFactory.cs'
        Marker = 'sqlserver'
        Lines = @(
            '            var connectionString = Environment.GetEnvironmentVariable("HELPDESK_TICKETS_SQL")'
            '                ?? "Data Source=helpdesk.db";'
            ''
        )
    },
    @{
        Scope = 'module'
        Path = 'Source/Hosting/MMCA.Helpdesk.Migrations.SqlServer.Tickets/DesignTimeSQLServerDbContextFactory.cs'
        Marker = 'sqlserver'
        Lines = @(
            '            var connectionString = Environment.GetEnvironmentVariable("HELPDESK_TICKETS_SQL")'
            '                ?? "Data Source=helpdesk_tickets.db";'
            ''
        )
    },
    @{
        Scope = 'app'
        Path = 'Source/Hosting/MMCA.Helpdesk.AppHost/Program.cs'
        Marker = 'sqlserver'
        Lines = @(
            '// SQLite is an in-process file: there is no container to declare and nothing to wait for, so'
            '// WithSqliteDataSource only injects the connection string the API host opens at startup. One'
            '// file, one tenant: routing a second tenant onto its own database needs a second file and its'
            '// own per-tenant override, which this shape leaves to you.'
            'var web = builder.AddProject<Projects.MMCA_Helpdesk_Web>("web")'
            '    .WithSqliteDataSource("Tickets", Path.Combine(builder.AppHostDirectory, "helpdesk.db"))'
            '    // Declares the readiness probe as this resource''s health check, which is what makes the'
            '    // WaitFor(web) below mean "wait until the API is HEALTHY" instead of "wait until its'
            '    // process started". MapDefaultEndpoints() serves /health/ready from the same'
            '    // MMCA.Common.Aspire pipeline the deployed readiness probe uses.'
            '    .WithHttpHealthCheck("/health/ready")'
            '    .WithExternalHttpEndpoints();'
            ''
        )
    }
)

function Add-EngineAlternatives {
    param(
        [string] $Root,
        [string] $TemplateName
    )

    $scope = if ($TemplateName -eq 'mmca-app') { 'app' } else { 'module' }
    $injected = 0

    foreach ($alternative in $engineAlternatives) {
        if ($alternative.Scope -ne 'both' -and $alternative.Scope -ne $scope) { continue }

        $full = Join-Path $Root $alternative.Path
        if (-not (Test-Path $full)) {
            throw "${TemplateName}: stage.ps1 declares an engine alternative for $($alternative.Path), which is not in staging. Either the file moved (update `$engineAlternatives) or its Scope is wrong."
        }

        Add-EngineAlternative -Path $full -Marker $alternative.Marker -Lines $alternative.Lines -TemplateName $TemplateName
        $injected++
    }

    if ($injected -eq 0) {
        throw "${TemplateName}: no engine alternative is scoped to this template, so its sqlite shape would emit SQL Server code. Check the Scope values in `$engineAlternatives."
    }

    Write-Host "${TemplateName}: $injected sqlite alternative(s) injected beside their SQL Server regions"
}

# ---- rewrites that apply to mmca-module only -----------------------------------------------------
# mmca-app generates the FIRST module of a solution and mmca-module generates a LATER one, and on one
# point those are not the same thing at all: the first module is the solution's Default data source
# and a later one never is. Default is decided by the host's top-level connection string, which names
# the first module's database, and under an orchestrator by whichever data-source call runs last,
# which build/add-module.ps1 keeps as the first module's for exactly this reason.
#
# It matters because the framework puts its Default-source-only tables (ScheduledJobs) in the model of
# whichever source IS Default. The seed's design-time factory names the same connection twice, top
# level and under its own logical name, so the resolver collapses it onto Default and the scaffolded
# model carries those tables: correct for the first module, and wrong for every later one. EF refuses
# to migrate a database whose model has pending changes, so a second module scaffolded that way does
# not produce a subtly different schema, it stops the host from starting.
#
# The seed cannot hold both shapes (it IS a one-module app, and its factory has to stay right), so the
# module-only shape is applied HERE, to the staged copy, the same way the engine alternatives are. An
# anchor that no longer matches exactly once throws rather than shipping a module template whose first
# migration is unusable.
$moduleOnlyRewrites = @(
    @{
        Path = 'Source/Hosting/MMCA.Helpdesk.Migrations.SqlServer.Tickets/DesignTimeSQLServerDbContextFactory.cs'
        Description = 'the design-time remark about collapsing onto the Default source'
        From = @'
/// The top-level connection string and the <c>Tickets</c> entry carry the SAME value on purpose. That
/// is exactly what the host injects for the <c>Tickets</c> data source at run time, and the resolver
/// collapses a logical name onto <c>Default</c> when the two connections match.
/// The scaffolded model therefore matches the running one, which matters for the framework tables that
/// are Default-source-only (<c>ScheduledJobs</c>). Point <c>HELPDESK_TICKETS_SQL</c> at a real database
/// to apply.
'@
        To = @'
/// This declares the <c>Tickets</c> data source plus a PLACEHOLDER top-level connection whose
/// identity matches no real database. A module added to an existing solution is never that
/// solution's <c>Default</c> source: the first module keeps that role. The placeholder matters
/// because the resolver seeds <c>Default</c> from a factory's single named entry when no top-level
/// connection exists (the same collapse a matching top-level value causes), which would scaffold the
/// Default-source-only framework tables (<c>ScheduledJobs</c>) into this module's migrations. They
/// are not in the running model, and EF refuses to migrate a database whose model has pending
/// changes, so the host would stop at startup rather than merely drift. The placeholder is never
/// used to connect. Point <c>HELPDESK_TICKETS_SQL</c> at a real database to apply.
'@
    },
    @{
        Path = 'Source/Hosting/MMCA.Helpdesk.Migrations.SqlServer.Tickets/DesignTimeSQLServerDbContextFactory.cs'
        Description = 'the top-level connection string a later module must not claim'
        From = @'
            options.DataSourceName = "Tickets";
            options.ConnectionStrings = new ConnectionStringSettings { SQLServerConnectionString = connectionString };
'@
        To = @'
            options.DataSourceName = "Tickets";

            // A deliberately unmatchable placeholder: with NO top-level connection the resolver seeds
            // Default from this factory's single named entry, which would pull the Default-source-only
            // framework tables (ScheduledJobs) into this module's migrations. Never used to connect.
            options.ConnectionStrings = new ConnectionStringSettings { SQLServerConnectionString = "Server=localhost;Database=__AppDefaultPlaceholder" };
'@
    }
)

function Convert-ModuleOnlyShapes {
    param(
        [string] $Root,
        [string] $TemplateName
    )

    foreach ($rewrite in $moduleOnlyRewrites) {
        $full = Join-Path $Root $rewrite.Path
        if (-not (Test-Path $full)) {
            throw "${TemplateName}: stage.ps1 declares a module-only rewrite for $($rewrite.Path), which is not in staging. The file moved; update `$moduleOnlyRewrites."
        }

        $text = Get-Content $full -Raw
        # Line endings are the staged file's, whatever they are, so the here-strings above (which the
        # PowerShell parser normalises) cannot introduce a second style into one file.
        $newline = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
        $from = ($rewrite.From -split "`r?`n") -join $newline
        $to = ($rewrite.To -split "`r?`n") -join $newline

        $occurrences = ([regex]::Matches($text, [regex]::Escape($from))).Count
        if ($occurrences -ne 1) {
            throw "${TemplateName}: expected exactly one occurrence of $($rewrite.Description) in $($rewrite.Path), found $occurrences. The seed's design-time factory was reworded; update `$moduleOnlyRewrites in stage.ps1 rather than shipping a module template whose first migration scaffolds the Default source's tables."
        }

        Set-Content -Path $full -Value $text.Replace($from, $to) -NoNewline
    }

    Write-Host "${TemplateName}: $($moduleOnlyRewrites.Count) later-module rewrite(s) applied (this module is not the solution's Default data source)"
}

function Remove-TemplateMarkers {
    param([string] $Path)

    $text = Get-Content $Path -Raw
    if (-not $text -or $text -notmatch 'template:(begin|end)') { return 0 }

    $lines = $text -split "`r?`n"
    $newline = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $kept = @($lines | Where-Object { $_ -notmatch $markerPattern })
    Set-Content -Path $Path -Value ($kept -join $newline) -NoNewline

    return $lines.Count - $kept.Count
}

# The eager-load argument names a navigation the adopter's aggregate may not have. In mmca-app and
# mmca-module that is the --flat axis; in the slice templates it is --child-collection. Same edit,
# two symbols. The false branch passes an EMPTY list rather than omitting the argument:
# IEntityReader.GetByIdAsync declares includes as a required parameter, so dropping the line trades
# one compile error (missing navigation) for another (CS7036).
function Convert-EagerLoad {
    param(
        [string] $Path,
        [string] $Condition
    )

    $source = Get-Content $Path -Raw
    $includeLine = '(?m)^([ \t]*)(includes: \[nameof\(Ticket\.Comments\)\],)(\r?\n)'
    $hits = ([regex]::Matches($source, $includeLine)).Count
    if ($hits -eq 0) { return 0 }

    # Directives at column 0, reusing the line's own terminator so the staged file does not acquire
    # mixed line endings. The engine strips whole directive lines in both branches, so the emitted C#
    # keeps the argument's own indentation and no comment survives into generated code.
    $conditional = "//#if ($Condition)`$3`$1`$2`$3//#else`$3`$1includes: [],`$3//#endif`$3"
    Set-Content -Path $Path -Value ([regex]::Replace($source, $includeLine, $conditional)) -NoNewline

    return $hits
}

# ---- per-shape rewrites of a single line ---------------------------------------------------------
# --no-description and --no-owner drop a property that also sits inside comma-separated LISTS: the
# domain factory and the private constructor, two positional records, a handful of call sites, a
# LoggerMessage template, the typed API client's signatures and its anonymous request bodies. C#
# forbids a trailing comma in an invocation, a parameter list and a positional record (it allows one
# in an object initializer and in a collection expression), so a whole-line marker region cannot drop
# a MIDDLE or LAST element of one: it would leave a dangling comma or eat the closing parenthesis.
#
# Wherever reshaping the seed was possible it was preferred, and it is what covers most of the two
# axes: object initializers grew a trailing comma on every member so plain markers work, and the two
# UI pages compose their required-field check one accumulator line at a time. What is left is the
# lists themselves. Those the SEED keeps on one line, deliberately, and this rewrites that line into
# one conditional block per combination of the axes it carries.
#
# The variants are DERIVED, never hand-written: each axis contributes a regex naming its own segment
# of the line (its separator included), and removing that segment IS the variant. Nothing here knows
# what a line means, which is why the same helper covers signatures, records, calls, log templates
# and anonymous objects. A segment that fails to match throws rather than silently emitting a branch
# identical to the full line, which would ship the dropped axis into a shape that asked for it gone.
#
# Emitted as INDEPENDENT #if blocks rather than one #if/#elseif/#else chain: .razor's conditional
# form is a pseudo-comment pair (@*#if ... ##endif*@) and independent blocks need only the two tokens
# that the marker conversion above already proves the engine honors in every staged file type.
function Convert-OptionalAxisLine {
    param(
        [string] $Path,
        [string] $Anchor,
        [string] $DropDescription,
        [string] $DropOwner,
        [int] $ExpectedHits,
        [string] $TemplateName
    )

    $ext = [IO.Path]::GetExtension($Path).ToLowerInvariant()
    if (-not $markerStyles.ContainsKey($ext)) {
        throw "${TemplateName}: $Path needs a per-shape line rewrite but '$ext' has no conditional syntax mapped in stage.ps1."
    }
    $style = $markerStyles[$ext]

    # Order matters only in that both segments must still match after the other one is removed, which
    # is why each pattern carries its own separator rather than relying on what is left around it.
    $axes = @()
    if ($DropDescription) { $axes += @{ Symbol = 'noDescription'; Pattern = $DropDescription } }
    if ($DropOwner) { $axes += @{ Symbol = 'noOwner'; Pattern = $DropOwner } }
    if ($axes.Count -eq 0) {
        throw "${TemplateName}: $Path declares a per-shape rewrite with no axis segment. Give it a description or an owner segment, or drop the entry."
    }

    $text = Get-Content $Path -Raw
    $newline = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $lines = $text -split "`r?`n"

    $out = [System.Collections.Generic.List[string]]::new()
    $hits = 0
    $emitted = 0

    foreach ($line in $lines) {
        if ($line -notmatch $Anchor) {
            $out.Add($line)
            continue
        }

        $hits++
        $indent = [regex]::Match($line, '^[ \t]*').Value

        for ($combination = 0; $combination -lt (1 -shl $axes.Count); $combination++) {
            $variant = $line
            $terms = @()

            for ($a = 0; $a -lt $axes.Count; $a++) {
                if (($combination -band (1 -shl $a)) -ne 0) {
                    $shrunk = [regex]::Replace($variant, $axes[$a].Pattern, '')
                    if ($shrunk -eq $variant) {
                        throw @"
${TemplateName}: in $Path the per-shape segment /$($axes[$a].Pattern)/ does not match the anchored line
  $($line.Trim())
so the '$($axes[$a].Symbol)' branch would be identical to the full line and would ship the dropped
axis into a shape that asked for it gone. Fix the segment pattern in stage.ps1, or the seed's line.
"@
                    }
                    $variant = $shrunk
                    $terms += $axes[$a].Symbol
                } else {
                    $terms += "!$($axes[$a].Symbol)"
                }
            }

            $out.Add($indent + ($style.If -f ($terms -join ' && ')))
            $out.Add($variant)
            $out.Add($indent + $style.EndIf)
            $emitted++
        }
    }

    if ($hits -ne $ExpectedHits) {
        throw "${TemplateName}: expected $ExpectedHits line(s) matching /$Anchor/ in $Path, found $hits. The seed's line moved or was reflowed; update the anchor in stage.ps1 rather than shipping a template that still names the dropped axis."
    }

    Set-Content -Path $Path -Value ($out -join $newline) -NoNewline

    return $emitted
}

# Scope is which templates stage the file: mmca-module carries only Source/Modules/Tickets,
# Tests/Modules/Tickets and the migrations project, so the UI host's entries are mmca-app only. A
# declared file that is missing from a scope throws, which is what turns a moved file into a loud
# staging failure rather than a template that quietly keeps the axis it was told to drop.
$optionalAxisLines = @(
    @{
        Scope = 'both'
        Path = 'Source/Modules/Tickets/MMCA.Helpdesk.Tickets.Domain/Tickets/Ticket.cs'
        Anchor = 'private Ticket\(string title, string description, int requesterUserId\)'
        Description = 'string description, '
        Owner = ', int requesterUserId'
        Hits = 1
    },
    @{
        Scope = 'both'
        Path = 'Source/Modules/Tickets/MMCA.Helpdesk.Tickets.Domain/Tickets/Ticket.cs'
        Anchor = 'public static Result<Ticket> Create\(TicketIdentifierType\? id, string title, string description, int requesterUserId\)'
        Description = 'string description, '
        Owner = ', int requesterUserId'
        Hits = 1
    },
    @{
        Scope = 'both'
        Path = 'Source/Modules/Tickets/MMCA.Helpdesk.Tickets.Domain/Tickets/Ticket.cs'
        Anchor = 'Result\.Combine\(TicketInvariants\.EnsureTitleIsValid\(title, nameof\(\w+\)\), TicketInvariants\.EnsureDescriptionIsValid\(description, nameof\(\w+\)\)\);'
        Description = ', TicketInvariants\.EnsureDescriptionIsValid\(description, nameof\(\w+\)\)'
        Owner = ''
        Hits = 2
    },
    @{
        Scope = 'both'
        Path = 'Source/Modules/Tickets/MMCA.Helpdesk.Tickets.Domain/Tickets/Ticket.cs'
        Anchor = 'var ticket = new Ticket\(title, description, requesterUserId\)'
        Description = 'description, '
        Owner = ', requesterUserId'
        Hits = 1
    },
    @{
        Scope = 'both'
        Path = 'Source/Modules/Tickets/MMCA.Helpdesk.Tickets.Domain/Tickets/Ticket.cs'
        Anchor = 'public Result UpdateDetails\(string title, string description\)'
        Description = ', string description'
        Owner = ''
        Hits = 1
    },
    @{
        Scope = 'both'
        Path = 'Source/Modules/Tickets/MMCA.Helpdesk.Tickets.Application/Tickets/UseCases/Create/TicketCreateRequestMapper.cs'
        Anchor = 'Ticket\.Create\(id: null, title: request\.Title, description: request\.Description, requesterUserId: request\.RequesterUserId\)'
        Description = 'description: request\.Description, '
        Owner = ', requesterUserId: request\.RequesterUserId'
        Hits = 1
    },
    @{
        Scope = 'both'
        Path = 'Source/Modules/Tickets/MMCA.Helpdesk.Tickets.Application/Tickets/UseCases/Create/CreateTicketHandler.cs'
        Anchor = 'new TicketOpenedIntegrationEvent\(entity\.Id, entity\.RequesterUserId\),'
        Description = ''
        Owner = ', entity\.RequesterUserId'
        Hits = 1
    },
    @{
        Scope = 'both'
        Path = 'Source/Modules/Tickets/MMCA.Helpdesk.Tickets.Shared/Tickets/IntegrationEvents/TicketOpenedIntegrationEvent.cs'
        Anchor = 'public sealed record class TicketOpenedIntegrationEvent\(TicketIdentifierType TicketId, int RequesterUserId\)'
        Description = ''
        Owner = ', int RequesterUserId'
        Hits = 1
    },
    @{
        Scope = 'both'
        Path = 'Source/Modules/Tickets/MMCA.Helpdesk.Tickets.Application/Tickets/IntegrationEventHandlers/TicketOpenedHandler.cs'
        Anchor = 'LogTicketOpened\(logger, integrationEvent\.TicketId, integrationEvent\.RequesterUserId, integrationEvent\.SchemaVersion\);'
        Description = ''
        Owner = ', integrationEvent\.RequesterUserId'
        Hits = 1
    },
    @{
        # The LoggerMessage template and the partial method below it have to move together: the source
        # generator matches placeholders to parameter names, so dropping one without the other fails
        # the generated app's build rather than merely logging a stale field.
        Scope = 'both'
        Path = 'Source/Modules/Tickets/MMCA.Helpdesk.Tickets.Application/Tickets/IntegrationEventHandlers/TicketOpenedHandler.cs'
        Anchor = 'Message = "Integration event: ticket \{TicketId\} opened by user \{RequesterUserId\} \(schema v\{SchemaVersion\}\)\."\)\]'
        Description = ''
        Owner = ' by user \{RequesterUserId\}'
        Hits = 1
    },
    @{
        Scope = 'both'
        Path = 'Source/Modules/Tickets/MMCA.Helpdesk.Tickets.Application/Tickets/IntegrationEventHandlers/TicketOpenedHandler.cs'
        Anchor = 'private static partial void LogTicketOpened\(ILogger logger, int ticketId, int requesterUserId, int schemaVersion\);'
        Description = ''
        Owner = ', int requesterUserId'
        Hits = 1
    },
    @{
        # The update slice is the framework's generic write side: the applier is the only file left
        # that names a ticket field, because the command carries the request whole and the controller
        # hands it over untouched.
        Scope = 'both'
        Path = 'Source/Modules/Tickets/MMCA.Helpdesk.Tickets.Application/Tickets/UseCases/Update/TicketUpdateApplier.cs'
        Anchor = 'return Task\.FromResult\(entity\.UpdateDetails\(request\.Title, request\.Description\)\);'
        Description = ', request\.Description'
        Owner = ''
        Hits = 1
    },
    @{
        Scope = 'both'
        Path = 'Tests/Modules/Tickets/MMCA.Helpdesk.Tickets.Domain.Tests/Tickets/TicketTests.cs'
        Anchor = 'Ticket\.Create\(id: null, "Cannot log in", "The login page returns a 500\.", requesterUserId: 42\);'
        Description = ', "The login page returns a 500\."'
        Owner = ', requesterUserId: 42'
        Hits = 1
    },
    @{
        Scope = 'both'
        Path = 'Tests/Modules/Tickets/MMCA.Helpdesk.Tickets.Domain.Tests/Tickets/TicketTests.cs'
        Anchor = 'Ticket\.Create\(id: null, "[^"]*", "Description", requesterUserId: 1\)'
        Description = ', "Description"'
        Owner = ', requesterUserId: 1'
        Hits = 3
    },
    @{
        Scope = 'both'
        Path = 'Tests/Modules/Tickets/MMCA.Helpdesk.Tickets.Domain.Tests/Tickets/TicketTests.cs'
        Anchor = 'ticket\.UpdateDetails\("[^"]*", "New description"\);'
        Description = ', "New description"'
        Owner = ''
        Hits = 2
    },
    @{
        Scope = 'both'
        Path = 'Tests/Modules/Tickets/MMCA.Helpdesk.Tickets.Application.Tests/Concurrency/TicketConcurrencyTokenTests.cs'
        Anchor = 'Ticket\.Create\(id: null, "Cannot log in", "The login page returns a 500\.", requesterUserId: 42\)'
        Description = ', "The login page returns a 500\."'
        Owner = ', requesterUserId: 42'
        Hits = 1
    },
    @{
        Scope = 'both'
        Path = 'Tests/Modules/Tickets/MMCA.Helpdesk.Tickets.Application.Tests/Projections/TicketDTOProjectorTests.cs'
        Anchor = 'Ticket\.Create\(id: null, "Cannot log in", "The login page returns a 500\.", requesterUserId: 42\)'
        Description = ', "The login page returns a 500\."'
        Owner = ', requesterUserId: 42'
        Hits = 1
    },
    @{
        # The frozen wire contract. Everything else in the literal is an ordinary symbol
        # substitution, and the base compares members as an unordered set, so this one member is the
        # only part of it any flag can change. Scope 'app' because mmca-module ships no
        # ArchitectureTests.cs: a generated module's event joins the app's existing literal, which is
        # what build/add-module.ps1 appends to.
        Scope = 'app'
        Path = 'Tests/Architecture/MMCA.Helpdesk.Architecture.Tests/ArchitectureTests.cs'
        Anchor = '"MMCA\.Helpdesk\.Tickets\.Shared\.Tickets\.IntegrationEvents\.TicketOpenedIntegrationEvent \{ RequesterUserId:Int32, TicketId:Int32 \}",'
        Description = ''
        Owner = 'RequesterUserId:Int32, '
        Hits = 1
    },
    @{
        Scope = 'app'
        Path = 'Source/Hosts/UI/MMCA.Helpdesk.UI.Web/Services/HelpdeskApiClient.cs'
        Anchor = 'public Task<Result<TicketDTO>> CreateTicketAsync\(string title, string description, int requesterUserId, CancellationToken cancellationToken = default\)'
        Description = 'string description, '
        Owner = 'int requesterUserId, '
        Hits = 1
    },
    @{
        Scope = 'app'
        Path = 'Source/Hosts/UI/MMCA.Helpdesk.UI.Web/Services/HelpdeskApiClient.cs'
        Anchor = '\.PostAsJsonAsync\("/Tickets", new \{ Title = title, Description = description, RequesterUserId = requesterUserId \}, cancellationToken\)'
        Description = ', Description = description'
        Owner = ', RequesterUserId = requesterUserId'
        Hits = 1
    },
    @{
        Scope = 'app'
        Path = 'Source/Hosts/UI/MMCA.Helpdesk.UI.Web/Services/HelpdeskApiClient.cs'
        Anchor = 'public Task<Result> UpdateTicketAsync\(int id, string title, string description, byte\[\] rowVersion, CancellationToken cancellationToken = default\)'
        Description = 'string description, '
        Owner = ''
        Hits = 1
    },
    @{
        Scope = 'app'
        Path = 'Source/Hosts/UI/MMCA.Helpdesk.UI.Web/Services/HelpdeskApiClient.cs'
        # Anchored on the whole PUT call, not on the anonymous object alone: the POST rewritten above
        # leaves a branch whose body IS "new { Title = title, Description = description }", so the
        # shorter pattern would match a line this pass had just emitted.
        Anchor = 'ConditionalPut\(string\.Create\(CultureInfo\.InvariantCulture, \$"/Tickets/\{id\}"\), new \{ Title = title, Description = description \}, rowVersion\)'
        Description = ', Description = description'
        Owner = ''
        Hits = 1
    },
    @{
        Scope = 'app'
        Path = 'Source/Hosts/UI/MMCA.Helpdesk.UI.Web/Components/Pages/Tickets.razor'
        Anchor = 'await Api\.CreateTicketAsync\(_title, _description, requesterUserId: 1\);'
        Description = ', _description'
        Owner = ', requesterUserId: 1'
        Hits = 1
    },
    @{
        Scope = 'app'
        Path = 'Source/Hosts/UI/MMCA.Helpdesk.UI.Web/Components/Pages/TicketDetail.razor'
        Anchor = 'await Api\.UpdateTicketAsync\(Id, _title, _description, ticket\.RowVersion\);'
        Description = ', _description'
        Owner = ''
        Hits = 1
    }
)

function Convert-OptionalAxisLines {
    param(
        [string] $Root,
        [string] $TemplateName
    )

    $rewritten = 0
    $blocks = 0

    foreach ($edit in $optionalAxisLines) {
        if ($edit.Scope -eq 'app' -and $TemplateName -ne 'mmca-app') { continue }

        $full = Join-Path $Root $edit.Path
        if (-not (Test-Path $full)) {
            throw "${TemplateName}: stage.ps1 declares a per-shape line rewrite for $($edit.Path), which is not in staging. Either the file moved (update `$optionalAxisLines) or its Scope is wrong."
        }

        $blocks += Convert-OptionalAxisLine `
            -Path $full `
            -Anchor $edit.Anchor `
            -DropDescription $edit.Description `
            -DropOwner $edit.Owner `
            -ExpectedHits $edit.Hits `
            -TemplateName $TemplateName

        $rewritten += $edit.Hits
    }

    Write-Host "${TemplateName}: $rewritten line(s) rewritten into $blocks --no-description / --no-owner conditional block(s)"
}

# Every .resx carries the standard ResX schema block, and inside it sits
#
#     <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
#
# which is boilerplate, not the child concept. --child replaces "comment" by dumb substring match, so
# an adopter passing --child Item would get name="item" in the schema of every resource file: not an
# error, not a warning, just quietly wrong XML that nobody reads until it matters. The attribute is
# rewritten with an XML character reference, which every parser resolves back to exactly "comment"
# while carrying no substring for the engine to match. Staged copies only; the seed keeps plain text.
function Protect-ResxSchemaToken {
    param(
        [string] $Root,
        [string] $TemplateName
    )

    $hazard = 'name="comment"'
    $safe = 'name="&#99;omment"'
    $patched = 0

    foreach ($file in Get-ChildItem -Path $Root -Recurse -File -Force -Filter '*.resx') {
        $text = Get-Content $file.FullName -Raw
        if (-not $text.Contains($hazard)) { continue }
        Set-Content -Path $file.FullName -Value $text.Replace($hazard, $safe) -NoNewline
        $patched++
    }

    if ($patched -eq 0) {
        throw "${TemplateName}: no staged .resx carries the '$hazard' schema attribute. Either the resource files moved or the ResX schema changed; confirm before dropping this guard, because without it --child mangles the schema of every .resx."
    }

    Write-Host "${TemplateName}: ResX schema 'comment' attribute protected in $patched file(s)"
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
$overlayExpectations = @(
    'README.md'
    '.gitignore'
    'local.props'
    '.github/workflows/ci.yml'
    'build/add-module.ps1'
    # The three whole-file variants. Neither .json nor .md carries marker regions in this seed (the
    # sqlite appsettings differs structurally rather than by a line, and prose has no conditional
    # form), so each ships beside its default and template.json renames it into place under the flag
    # that asks for it. A variant that fails to land is silent: the default file stays and the
    # generated app is quietly wired for the shape the adopter did not ask for.
    'README.standalone.md'
    'Source/Hosts/MMCA.Helpdesk.Web/appsettings.sqlite.json'
    'Source/Hosts/UI/MMCA.Helpdesk.UI.Web/appsettings.standalone.json'
)

foreach ($expected in $overlayExpectations) {
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
# SA1211 is the same problem one level down, on the identifier-alias file. The seed's two aliases sort
# TicketCommentIdentifierType before TicketIdentifierType; --child Line renames them to
# ShipmentLineIdentifierType and ShipmentIdentifierType, which sort the other way round. No checked-in
# order satisfies both, and that file is linked into EVERY project, so one wrong name fails the whole
# solution rather than one file.
#
# IDE0021 is the same shape of problem on the shape flags rather than the renames. The aggregate's
# private constructor assigns one property per axis, so --no-status --no-description --no-owner
# together leave it with a single statement, and the baseline requires an expression body for that.
# No checked-in form is right for every shape: a block body is correct for every combination but
# that one, and an expression body compiles for no other. The seed keeps the block and its full
# strictness; only generated apps see this relaxed, and the note below says how to put it back.
#
# There is deliberately no post-action that runs `dotnet format` for the adopter. dotnet new's
# run-script post action (actionId 3A7C4B45-1F5D-4A30-959A-51B88E82B5D2) is gated on --allow-scripts,
# whose DEFAULT is Prompt: declaring one would make every non-interactive `dotnet new mmca-app` stop
# and ask, including this pack's own smoke run and any adopter's scripted scaffold. It would also run
# before the app has ever been restored or built, which is not a state `dotnet format analyzers` can
# work from. The delta below plus the command in the generated README is the supported answer.
#
# This is appended to the STAGED copy, never to the seed's own .editorconfig: that file stays the
# single source of the shared analyzer baseline that compare-analyzer-config.ps1 enforces across the
# four repos. Three rules are relaxed (the delta ADDS entries; none of the three appears among the
# seed's own explicit severities, which are all untouched), and the generated app carries the one
# command that restores SA1210/SA1211 plus the by-hand note for IDE0021.
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
# in alphabetical order and SA1210 would fail your first build. SA1211 is the same story for the
# identifier-alias file, whose two aliases sort differently depending on the aggregate and child
# names you asked for.
#
# Sort them with:
#
#     dotnet format analyzers <YourApp>.slnx --diagnostics SA1210 SA1211 --severity info
#
# Every dotnet new mmca-command / mmca-query slice arrives with the same skew, so either re-run
# that command after scaffolding, or leave this block in place until you have stopped scaffolding
# and then delete it to get the full baseline back.
#
# IDE0021 is here for the shape flags rather than the renames: the aggregate's private constructor
# assigns one property per optional axis, so turning several of them off can leave it with a single
# statement, which the baseline would then require you to write as an expression body. Fold it to
# an expression body by hand (or add your own second property) and you can delete this line.
#
# Only these three rules are relaxed. Every other analyzer stays at error severity.
# ---------------------------------------------------------------------------------------------
[*.cs]
dotnet_diagnostic.SA1210.severity = suggestion
dotnet_diagnostic.SA1211.severity = suggestion
dotnet_diagnostic.IDE0021.severity = suggestion
'@

# Idempotent: staging may be re-run over an existing tree.
if ((Get-Content $editorConfig -Raw) -notmatch 'SCAFFOLD DELTA') {
    Add-Content -Path $editorConfig -Value $delta -NoNewline
}

# The .editorconfig is the one staged file declared copyOnly in template.json, so NO symbol
# replacement runs over it. That is deliberate: its rule descriptions contain the very words this
# template renames ("comment" appears in nine analyzer descriptions), and substring replacement would
# turn "XML comment analysis disabled" into "XML item analysis disabled" for anyone passing --child.
# The price is that any seed token left in the file would leak into the generated app verbatim, and
# there is exactly one: the header describing the four-repo shared baseline. Rewrite it, then prove
# nothing else is left.
$editorConfigText = Get-Content $editorConfig -Raw
$sharedHeaderPattern = '(?m)^# SHARED ANALYZER BASELINE\r?\n(?:^#[^\r\n]*\r?\n)+?^# ={10,}\r?\n'
$sharedHeaderHits = ([regex]::Matches($editorConfigText, $sharedHeaderPattern)).Count

if ($sharedHeaderHits -eq 1) {
    $scaffoldHeader = @(
        '# ANALYZER BASELINE'
        '# Scaffolded by dotnet new mmca-app. In the reference seed this block is kept'
        '# byte-identical across the framework repos; here it is yours, edit it freely.'
        '# The SCAFFOLD DELTA at the bottom of this file is the only rule the scaffold'
        '# relaxes, and it says how to put it back.'
        '# ============================================================================='
    )
    $newline = if ($editorConfigText.Contains("`r`n")) { "`r`n" } else { "`n" }
    $replacement = ($scaffoldHeader -join $newline) + $newline
    Set-Content -Path $editorConfig -NoNewline `
        -Value ([regex]::Replace($editorConfigText, $sharedHeaderPattern, $replacement))
} elseif ($sharedHeaderHits -ne 0) {
    throw "Found $sharedHeaderHits SHARED ANALYZER BASELINE headers in the staged .editorconfig; expected one. Update the pattern in stage.ps1."
}

$editorConfigLeaks = @(Select-String -Path $editorConfig -Pattern 'MMCA\.Helpdesk|Helpdesk|Ticket' -CaseSensitive |
    ForEach-Object { "line $($_.LineNumber): $($_.Line.Trim())" })
if ($editorConfigLeaks) {
    throw @"
The staged .editorconfig still names the seed, and it is copyOnly so no rename will reach it:
  $($editorConfigLeaks -join "`n  ")
Either rewrite those lines here or drop the copyOnly modifier from .template.config/template.json.
"@
}

# ---- the wire-contract freeze SHIPS, and is guarded rather than deleted -------------------------
# It used to be deleted here, on the grounds that no single frozen literal is right for every name
# the scaffold can be given. Two things changed. IntegrationEventContractTestsBase now compares each
# event's members as a SET rather than a sequence, so the aggregate's own Id moving position is no
# longer a difference; and every other part of the literal (the namespace, the event type name) is
# an ordinary symbol substitution that the template already performs everywhere else. What is left
# is the one member the shape flags can remove, RequesterUserId, and that is a comma-separated list
# like any other: $optionalAxisLines rewrites it.
#
# So the generated app arrives with its OWN contract already frozen, under its own names, green on
# the first test run. The guard below is what keeps that true: a class the staging pass cannot find
# is a template about to ship an unfrozen wire contract with a README that says it is frozen.
$archTests = Join-Path $appStaging 'Tests/Architecture/MMCA.Helpdesk.Architecture.Tests/ArchitectureTests.cs'
if (-not (Test-Path $archTests)) {
    throw "Expected $archTests in staging. The architecture-fitness map moved; update stage.ps1."
}

$archSource = Get-Content $archTests -Raw
$contractClass = '(?ms)public sealed class IntegrationEventContractTests\s*:\s*IntegrationEventContractTestsBase\r?\n\{.*?\r?\n\}\r?\n\r?\n'
$matchCount = ([regex]::Matches($archSource, $contractClass)).Count

if ($matchCount -ne 1) {
    throw "Expected exactly one IntegrationEventContractTests class in ArchitectureTests.cs, found $matchCount. The generated app's README promises a wire contract that is frozen on arrival; update the pattern in stage.ps1 rather than shipping one that is not."
}

# ---- derived: the shape marker regions, the per-shape lines, and the eager-load fallback --------
Add-EngineAlternatives -Root $appStaging -TemplateName 'mmca-app'
Convert-TemplateMarkers -Root $appStaging -TemplateName 'mmca-app'
Convert-OptionalAxisLines -Root $appStaging -TemplateName 'mmca-app'
Protect-ResxSchemaToken -Root $appStaging -TemplateName 'mmca-app'

$appEagerLoads = 0
foreach ($handler in Get-ChildItem -Path $appStaging -Recurse -File -Force -Filter '*.cs') {
    $appEagerLoads += Convert-EagerLoad -Path $handler.FullName -Condition '!flat'
}
if ($appEagerLoads -eq 0) {
    throw "mmca-app: no staged handler eager-loads the aggregate's child collection, so --flat would either be a no-op there or ship code naming a navigation it just removed. The 'includes: [nameof(Ticket.Comments)]' anchor moved; update Convert-EagerLoad in stage.ps1."
}
Write-Host "mmca-app: $appEagerLoads eager-load argument(s) made conditional on --flat"

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

# The command slice ships its validator alongside the command, and that is load-bearing rather than
# generous. The generated app's CommandValidatorCoverageTests fails any handled, data-carrying command
# with no validator, and the rule's allowlist matches full type names or namespace prefixes only: an
# app cannot pre-allow a slice whose name the adopter has not chosen yet. So a scaffolded command that
# arrived without a validator would turn the adopter's next test run red, in a file they did not write.
# Shipping the seed's identifier-only validator makes the slice green on arrival and leaves the rules
# to grow with the payload. Queries are not in that rule's scope, so mmca-query ships two files.
$slices = @(
    @{ Name = 'mmca-command'; Source = 'Delete';  Files = @('DeleteTicketCommand.cs', 'DeleteTicketCommandValidator.cs', 'DeleteTicketHandler.cs') },
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

    # A slice has neither a --flat nor a --no-status axis: it is one use case, and what it needs from
    # the child collection is already covered by --child-collection below. So any marker the seed
    # grows in these two files is STRIPPED here (the content stays, only the marker lines go) rather
    # than converted to a condition on a symbol these templates do not declare.
    $strippedMarkers = 0
    foreach ($file in $slice.Files) {
        $strippedMarkers += Remove-TemplateMarkers -Path (Join-Path $target $file)
    }
    if ($strippedMarkers -gt 0) {
        Write-Host "$($slice.Name): $strippedMarkers template marker line(s) stripped (slices carry no axis symbols)"
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

    $includeMatches = Convert-EagerLoad -Path $handler -Condition 'hasChildCollection'
    if ($includeMatches -ne 1) {
        throw "Expected exactly one 'includes: [nameof(Ticket.Comments)]' line in the staged $($slice.Name) handler, found $includeMatches. The seed's $($slice.Source) handler changed shape; update the pattern in stage.ps1 rather than shipping a slice that names a collection the adopter's aggregate may not have."
    }

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

# The engine axis is DECLARED in two template.json files, once per template, and a condition on a
# symbol a template does not declare is silently false rather than an error. So a mmca-module that
# lost the declaration would keep converting its 'sqlite' markers, evaluate every one of them to
# false, and ship a module that is SQL-Server-shaped whatever --database says: it would generate, it
# would be added to the solution, and it would fail to compile inside a sqlite app on a configuration
# base that app does not reference. Asserted here rather than trusted, the same way mmca-app's
# copyOnly and whole-file-variant declarations are.
$moduleTemplateJson = Get-Content (Join-Path $moduleStaging '.template.config/template.json') -Raw

foreach ($declaration in @(
    @{ Pattern = '(?s)"database"\s*:\s*\{[^}]*"datatype"\s*:\s*"choice"'; What = 'the --database choice parameter' }
    @{ Pattern = '(?s)"useSqlite"\s*:\s*\{[^}]*"value"\s*:\s*"\(database == \\"sqlite\\"\)"'; What = 'the useSqlite computed symbol the markers condition on' }
    @{ Pattern = '(?s)"engineName"\s*:\s*\{[^}]*"replaces"\s*:\s*"SqlServer"'; What = 'the engineName rename (migrations project, provider package, CreateSqlServer)' }
    @{ Pattern = '(?s)"engineNameUpper"\s*:\s*\{[^}]*"replaces"\s*:\s*"SQLServer"'; What = 'the engineNameUpper rename (design-time factory, DbContext, configuration base, settings keys)' }
    @{ Pattern = '"condition"\s*:\s*"\(useSqlite\)"'; What = 'the sqlite branch of the printed wire-up instructions' }
)) {
    if ($moduleTemplateJson -notmatch $declaration.Pattern) {
        throw "mmca-module's template.json no longer declares $($declaration.What). Without it a module generated for a sqlite app comes out SQL-Server-shaped, which does not compile there. Restore it in templates/mmca-module/.template.config/template.json."
    }
}

Add-EngineAlternatives -Root $moduleStaging -TemplateName 'mmca-module'
Convert-ModuleOnlyShapes -Root $moduleStaging -TemplateName 'mmca-module'
Convert-TemplateMarkers -Root $moduleStaging -TemplateName 'mmca-module' -StripLabels $moduleStripLabels
Convert-OptionalAxisLines -Root $moduleStaging -TemplateName 'mmca-module'
Protect-ResxSchemaToken -Root $moduleStaging -TemplateName 'mmca-module'

$moduleEagerLoads = 0
foreach ($handler in Get-ChildItem -Path $moduleStaging -Recurse -File -Force -Filter '*.cs') {
    $moduleEagerLoads += Convert-EagerLoad -Path $handler.FullName -Condition '!flat'
}
if ($moduleEagerLoads -eq 0) {
    throw "mmca-module: no staged handler eager-loads the aggregate's child collection. The 'includes: [nameof(Ticket.Comments)]' anchor moved; update Convert-EagerLoad in stage.ps1."
}
Write-Host "mmca-module: $moduleEagerLoads eager-load argument(s) made conditional on --flat"

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

# ---- guards on the two things that fail silently -------------------------------------------------
# 1. A marker that reached staging unconverted is invisible: the template packs, the app generates,
#    and the generated file carries "// template:begin child" as a comment forever. Convert-
#    TemplateMarkers throws on anything it cannot handle, so this only catches a tree it never saw.
$stagedRoots = @($appStaging, $moduleStaging) + ($slices | ForEach-Object { Join-Path $OutputPath $_.Name })

$survivingMarkers = @(
    foreach ($root in $stagedRoots) {
        Get-ChildItem -Path $root -Recurse -File -Force |
            ForEach-Object {
                $hit = Select-String -Path $_.FullName -Pattern 'template:(begin|end)' -List
                if ($hit) { "$($_.FullName.Substring($OutputPath.Length).TrimStart('\', '/')): $($hit.Line.Trim())" }
            }
    })

if ($survivingMarkers) {
    throw @"
Template markers survived staging in $($survivingMarkers.Count) file(s) and would ship as comments:
  $($survivingMarkers -join "`n  ")
"@
}

# The files declared copyOnly in mmca-app's template.json. No symbol replacement runs over them, so
# the prose hazards below cannot reach them; what they DO carry is guarded on its own terms further
# down, because a copyOnly file leaks any seed token it holds straight into the generated app.
$copyOnlyLeaves = @('.editorconfig', 'PageHeading.razor', 'SectionHeading.razor', 'add-module.ps1')

# 2. Three symbols replace an ordinary English word by substring, so every occurrence in the staged
#    trees that is NOT the concept gets mangled for anyone who passes the flag: --child on "comment",
#    --event-verb on "opened", --title on "title". The seed is worded to keep them out ("code notes",
#    "children", "disabled", "heading", "browser tab"), the copyOnly files above are exempt by
#    construction, and the ResX schema attribute is escaped above. These are the English inflections
#    that can only ever be prose, never the concept: an "entitled"/"subtitle"/"retitle" is never the
#    aggregate's text property, and a "reopen" is never the creation event's verb.
$hazardPattern = 'commented|commenting|commenter|commentary|entitled|Entitled|subtitle|Subtitle|retitle|Retitle|reopen|Reopen'
$hazards = @(
    foreach ($root in $stagedRoots) {
        Get-ChildItem -Path $root -Recurse -File -Force |
            Where-Object { $copyOnlyLeaves -notcontains $_.Name } |
            ForEach-Object {
                $hit = Select-String -Path $_.FullName -Pattern $hazardPattern -CaseSensitive -List
                if ($hit) { "$($_.FullName.Substring($OutputPath.Length).TrimStart('\', '/')) line $($hit.LineNumber): $($hit.Line.Trim())" }
            }
    })

if ($hazards) {
    throw @"
Prose using "comment", "title" or "opened" as an English word survived into staging. --child,
--title and --event-verb each replace one of those substrings, so generating with --child Item /
--title Name / --event-verb Created rewrites these into nonsense. Reword them in the SEED (code
notes, disabled, children, heading, browser tab, created):
  $($hazards -join "`n  ")
"@
}

$resxHazards = @(
    foreach ($root in $stagedRoots) {
        Get-ChildItem -Path $root -Recurse -File -Force -Filter '*.resx' |
            ForEach-Object {
                $hit = Select-String -Path $_.FullName -Pattern 'name="comment"' -CaseSensitive -List
                if ($hit) { $_.FullName.Substring($OutputPath.Length).TrimStart('\', '/') }
            }
    })

if ($resxHazards) {
    throw "The ResX schema 'comment' attribute is unescaped in: $($resxHazards -join ', '). Protect-ResxSchemaToken did not reach these files."
}

# 3. The .editorconfig protection is a template.json declaration, not something staging can enforce
#    by copying. Losing it is silent in exactly the same way: the file still ships, just mangled.
$appTemplateJson = Get-Content (Join-Path $appStaging '.template.config/template.json') -Raw
if ($appTemplateJson -notmatch '(?s)"copyOnly"\s*:\s*\[[^\]]*\.editorconfig') {
    throw "mmca-app's template.json no longer declares .editorconfig under a copyOnly modifier. Without it --child rewrites the analyzer rule descriptions in every generated app."
}

# 4. --title replaces "Title" by substring, and the Blazor browser-tab element's tag name ENDS in that
#    word, so any razor file that writes the tag by hand ships <PageCustomerName> to anyone who passes
#    --title CustomerName. That is not a warning and not a mangled string: it is a UI host that does
#    not compile. Razor markup has no per-occurrence escape form (the ResX trick of an XML character
#    reference has no razor equivalent), so the seed keeps exactly ONE file that writes the tag, the
#    copyOnly PageHeading wrapper, and every page goes through it. Anything else carrying the string
#    is the regression this guard exists to name.
$wrapperRelative = 'Source/Hosts/UI/MMCA.Helpdesk.UI.Web/Components'
$tagWrapperLeaf = 'PageHeading.razor'

$stagedTagWrapper = Join-Path $appStaging "$wrapperRelative/$tagWrapperLeaf"
if (-not (Test-Path $stagedTagWrapper)) {
    throw "$tagWrapperLeaf did not reach mmca-app staging, so the guard below would pass having checked nothing. The UI host's browser-tab wrapper moved; update stage.ps1 and the copyOnly entry in .template.config/template.json together."
}

$tagBearers = @(
    foreach ($root in $stagedRoots) {
        Get-ChildItem -Path $root -Recurse -File -Force |
            Where-Object { $_.Name -ne $tagWrapperLeaf } |
            ForEach-Object {
                $hit = Select-String -Path $_.FullName -Pattern 'PageTitle' -SimpleMatch -CaseSensitive -List
                if ($hit) { "$($_.FullName.Substring($OutputPath.Length).TrimStart('\', '/')) line $($hit.LineNumber): $($hit.Line.Trim())" }
            }
    })

if ($tagBearers) {
    throw @"
The browser-tab element is written outside $tagWrapperLeaf in $($tagBearers.Count) staged file(s). Its
tag name ends in the word --title replaces, so those files stop compiling for anyone who renames the
aggregate's text property. Route them through the copyOnly wrapper instead:
  $($tagBearers -join "`n  ")
"@
}

# 5. Both wrappers are copyOnly, which is exactly what makes them dangerous in the other direction:
#    NO rename reaches them, so a seed token written into one ships verbatim into every generated
#    app. Same failure mode as the .editorconfig leak check above, and the same answer: prove the
#    files hold nothing but the two spellings they exist to isolate. Those two are scrubbed before
#    the check, because they are the point (the tag name legitimately ends in the --title word, and
#    the MudBlazor typography member legitimately begins with "sub" plus that word).
$wrapperLeaves = @($tagWrapperLeaf, 'SectionHeading.razor')
$wrapperTokens = 'MMCA\.Helpdesk|Helpdesk|Ticket|ticket|Comment|comment|Title|title|Opened|opened'

foreach ($leaf in $wrapperLeaves) {
    $wrapper = Join-Path $appStaging "$wrapperRelative/$leaf"
    if (-not (Test-Path $wrapper)) {
        throw "The copyOnly wrapper $leaf did not reach mmca-app staging at $wrapperRelative. Either it moved (update stage.ps1 and template.json together) or the UI host lost it, in which case the pages it replaced are writing the raw spellings again."
    }

    $wrapperLines = @(Get-Content $wrapper)
    $wrapperLeaks = @(
        for ($i = 0; $i -lt $wrapperLines.Count; $i++) {
            $scrubbed = $wrapperLines[$i] -replace 'PageTitle', '' -replace 'subtitle1', ''
            if ($scrubbed -cmatch $wrapperTokens) { "line $($i + 1): $($wrapperLines[$i].Trim())" }
        })

    if ($wrapperLeaks) {
        throw @"
$leaf is copyOnly, so no rename reaches it, and it still names the seed:
  $($wrapperLeaks -join "`n  ")
Reword those lines (the file must read as generic UI plumbing, with no reference to this app, its
module, its aggregate, or its child entity) or drop the copyOnly modifier from
.template.config/template.json and accept that --title mangles it.
"@
    }

    if ($appTemplateJson -notmatch "(?s)`"copyOnly`"\s*:\s*\[[^\]]*$([regex]::Escape($leaf))") {
        throw "mmca-app's template.json no longer declares $leaf under a copyOnly modifier. Without it --title rewrites the one spelling that file exists to hold, and the generated UI host does not compile."
    }
}

# 6. The wire-up script the overlay ships as build/add-module.ps1 in every generated app. It is
#    copyOnly for the same reason as the two razor wrappers and with the opposite consequence, which
#    is why its leak check is INVERTED relative to theirs.
#
#    Why copyOnly: it is a script ABOUT the scaffold, so it names the flags it passes through
#    (--title, --event-verb, --child) and prints prose about what they do. Token replacement would
#    rewrite those flag names per the adopter's own values and hand every generated app a script
#    that calls dotnet new with arguments no template declares. Shipping it verbatim is the only
#    form of it that works.
#
#    What that costs: nothing in it is renamed, so it cannot rely on ANY seed name being fixed up.
#    It discovers the solution file, the app namespace, the existing module and the four host and
#    test project paths at run time, from the tree it is standing in. A seed name written into it
#    would therefore not merely leak, it would name a path that does not exist in the app it runs
#    in. That is what this guard asserts: not "no prose hazards" but "no seed nouns at all".
$wireUpLeaf = 'add-module.ps1'
$stagedWireUp = Join-Path $appStaging "build/$wireUpLeaf"

if (-not (Test-Path $stagedWireUp)) {
    throw "$wireUpLeaf did not reach mmca-app staging at build/. Generated apps would ship no wire-up script, and mmca-module's printed instructions would be the only path to a second module. Check build/templates/overlay/mmca-app/build/."
}

$wireUpLeaks = @(Select-String -Path $stagedWireUp -Pattern 'MMCA\.Helpdesk|Helpdesk|Ticket' -CaseSensitive |
    ForEach-Object { "line $($_.LineNumber): $($_.Line.Trim())" })

if ($wireUpLeaks) {
    throw @"
$wireUpLeaf names the seed, and it is copyOnly so no rename reaches it. Worse, it runs INSIDE a
generated app where these names do not exist, so each of these is a path or a type that resolves to
nothing at run time:
  $($wireUpLeaks -join "`n  ")
Discover the real name instead (the solution file's basename, Source/Modules/*, the host globs).
"@
}

if ($appTemplateJson -notmatch "(?s)`"copyOnly`"\s*:\s*\[[^\]]*$([regex]::Escape($wireUpLeaf))") {
    throw "mmca-app's template.json no longer declares $wireUpLeaf under a copyOnly modifier. Without it --title / --event-verb / --child rewrite the very flag names the script passes through to dotnet new, and every generated app ships a wire-up script that cannot run."
}

# 7. The whole-file variants. Each one only reaches a generated app through a rename declared in
#    template.json, and a rename that is missing fails in the quietest way this template has: the
#    variant lands under its own name, the DEFAULT file lands too, and the app is wired for the shape
#    the adopter did not ask for while still building and testing green. So the declarations are
#    asserted here rather than trusted.
$variantRenames = @(
    @{ From = 'Source/Hosts/MMCA.Helpdesk.Web/appsettings.sqlite.json';            To = 'Source/Hosts/MMCA.Helpdesk.Web/appsettings.json' }
    @{ From = 'Source/Hosts/UI/MMCA.Helpdesk.UI.Web/appsettings.standalone.json';  To = 'Source/Hosts/UI/MMCA.Helpdesk.UI.Web/appsettings.json' }
    @{ From = 'README.standalone.md';                                              To = 'README.md' }
)

foreach ($variant in $variantRenames) {
    $declaration = '"' + [regex]::Escape($variant.From) + '"\s*:\s*"' + [regex]::Escape($variant.To) + '"'
    if ($appTemplateJson -notmatch $declaration) {
        throw "mmca-app's template.json declares no rename of $($variant.From) onto $($variant.To). Without it the variant ships beside the file it was meant to replace and the generated app keeps the default shape."
    }

    $excluded = '"' + [regex]::Escape($variant.From) + '"'
    if ($appTemplateJson -notmatch $excluded) {
        throw "mmca-app's template.json never excludes $($variant.From), so the shape that does NOT want it ships it as a stray file."
    }
}

# 8. Every staged XML file has to PARSE. This guard exists because two files in this pack did not,
#    for the same reason and with the same symptom: XML forbids a double hyphen inside a comment, so
#    a comment naming an option ("--no-aspire", "--local-mmca") makes the whole file invalid. MSBuild
#    does not fail on an unparsable optional import, it skips it, so the overlay's local.props sat
#    broken with a green build and generated apps quietly ignored the flag that emitted it. Nothing
#    else in the run can catch that: the template packs, the app generates, and the file is there.
#    Marker conversion is safe under this rule (a condition carries no double hyphen), which is why
#    the check runs on the STAGED trees and covers the seed's own files at the same time.
$xmlExtensions = @('.props', '.targets', '.csproj', '.config', '.slnx', '.xml', '.resx')

$malformedXml = @(
    foreach ($root in $stagedRoots) {
        Get-ChildItem -Path $root -Recurse -File -Force |
            Where-Object { $xmlExtensions -contains $_.Extension.ToLowerInvariant() } |
            ForEach-Object {
                try {
                    [xml] (Get-Content $_.FullName -Raw) | Out-Null
                } catch {
                    "$($_.FullName.Substring($OutputPath.Length).TrimStart('\', '/')): $($_.Exception.InnerException.Message ?? $_.Exception.Message)"
                }
            }
    })

if ($malformedXml) {
    throw @"
$($malformedXml.Count) staged XML file(s) do not parse, so MSBuild will skip or reject them:
  $($malformedXml -join "`n  ")
The usual cause is a double hyphen inside an XML comment (an option name written with its leading
dashes). Spell the option without them.
"@
}

# 9. The standalone README is the one variant whose content cannot be checked by building anything:
#    it is prose handed to an adopter who has no AppHost. A copy that kept the orchestration
#    instructions is worse than no README, because it tells them to run a project that is not there.
$standaloneReadme = Join-Path $appStaging 'README.standalone.md'
$standaloneLeaks = @(Select-String -Path $standaloneReadme -Pattern 'AppHost' -SimpleMatch -CaseSensitive |
    Where-Object { $_.Line -notmatch 'no AppHost|Aspire AppHost later|there is no' } |
    ForEach-Object { "line $($_.LineNumber): $($_.Line.Trim())" })

if ($standaloneLeaks) {
    throw @"
README.standalone.md is the README of a solution generated WITHOUT an orchestration project, and it
still instructs the reader to use one:
  $($standaloneLeaks -join "`n  ")
Reword those lines, or move them back into the default README.md.
"@
}

Write-Host "Guards passed: no surviving markers, no 'comment' / 'title' / 'opened' prose hazards, copyOnly declared for .editorconfig, both razor wrappers and $wireUpLeaf, $wireUpLeaf names no seed noun, and all $($variantRenames.Count) whole-file variants are declared and renamed."
Write-Host "Staging complete."
