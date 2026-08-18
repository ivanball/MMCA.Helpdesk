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
$markerConditions = @{
    'child'         = '!flat'
    'status'        = '!noStatus'
    'childStatus'   = '!(flat || noStatus)'
    'description'   = '!noDescription'
    'owner'         = '!noOwner'
    'statusOwner'   = '!(noStatus || noOwner)'
    'statusOrOwner' = '!(noStatus && noOwner)'
}

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
}

# One pattern, three comment shapes. .NET allows the same group name on alternate branches, so 'kind'
# and 'label' read back whichever branch matched. '//+' also picks up a marker written as '///', which
# is how a region inside an XML doc block is expressed without splitting the block in the seed.
$markerPattern = '^(?<indent>[ \t]*)(?://+\s*template:(?<kind>begin|end)\s+(?<label>\w+)|<!--\s*template:(?<kind>begin|end)\s+(?<label>\w+)\s*-->|@\*\s*template:(?<kind>begin|end)\s+(?<label>\w+)\s*\*@)[ \t]*$'

function Convert-TemplateMarkers {
    param(
        [string] $Root,
        [string] $TemplateName
    )

    $perAxis = @{}
    $regions = 0
    $touched = 0

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

            if ($match.Groups['kind'].Value -eq 'begin') {
                $stack.Push($label)
                $lines[$i] = $match.Groups['indent'].Value + ($style.If -f $markerConditions[$label])
                $regions++
                if (-not $perAxis.ContainsKey($label)) { $perAxis[$label] = 0 }
                $perAxis[$label]++
            } else {
                if ($stack.Count -eq 0) {
                    throw "${TemplateName}: $($file.FullName) line $($i + 1) closes a '$label' region that was never opened."
                }
                $open = $stack.Pop()
                if ($open -ne $label) {
                    throw "${TemplateName}: $($file.FullName) line $($i + 1) closes '$label' while '$open' is still open. Marker regions must nest."
                }
                $lines[$i] = $match.Groups['indent'].Value + $style.EndIf
            }
        }

        if ($stack.Count -gt 0) {
            throw "${TemplateName}: $($file.FullName) ends with $($stack.Count) unclosed marker region(s): $($stack -join ', ')."
        }

        Set-Content -Path $file.FullName -Value ($lines -join $newline) -NoNewline
        $touched++
    }

    $breakdown = ($perAxis.Keys | Sort-Object | ForEach-Object { "$_=$($perAxis[$_])" }) -join ', '
    Write-Host "$TemplateName marker regions converted: $regions across $touched file(s) ($breakdown)"
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
        Scope = 'both'
        Path = 'Source/Modules/Tickets/MMCA.Helpdesk.Tickets.Application/Tickets/UseCases/Update/UpdateTicketCommand.cs'
        Anchor = 'public sealed record UpdateTicketCommand\(TicketIdentifierType TicketId, string Title, string Description\)'
        Description = ', string Description'
        Owner = ''
        Hits = 1
    },
    @{
        Scope = 'both'
        Path = 'Source/Modules/Tickets/MMCA.Helpdesk.Tickets.Application/Tickets/UseCases/Update/UpdateTicketHandler.cs'
        Anchor = 'ticket\.UpdateDetails\(command\.Title, command\.Description\);'
        Description = ', command\.Description'
        Owner = ''
        Hits = 1
    },
    @{
        Scope = 'both'
        Path = 'Source/Modules/Tickets/MMCA.Helpdesk.Tickets.API/Controllers/TicketsController.cs'
        Anchor = 'new UpdateTicketCommand\(id, request\.Title, request\.Description\) \{ RowVersion = request\.RowVersion \},'
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
        Path = 'Tests/Modules/Tickets/MMCA.Helpdesk.Tickets.Application.Tests/Caching/TicketCacheInvalidationTests.cs'
        Anchor = 'new UpdateTicketCommand\(TicketId, "[^"]*", "Returns a 500\."\)'
        Description = ', "Returns a 500\."'
        Owner = ''
        Hits = 3
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
        Path = 'Tests/Modules/Tickets/MMCA.Helpdesk.Tickets.Application.Tests/Concurrency/TicketConcurrencyTokenTests.cs'
        Anchor = 'new UpdateTicketCommand\(TicketId, "[^"]*", "Returns a 500\."\)'
        Description = ', "Returns a 500\."'
        Owner = ''
        Hits = 2
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
        Scope = 'app'
        Path = 'Source/Hosts/UI/MMCA.Helpdesk.UI.Web/Services/HelpdeskApiClient.cs'
        Anchor = 'public async Task<TicketDTO\?> CreateTicketAsync\(string title, string description, int requesterUserId, CancellationToken cancellationToken = default\)'
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
        Anchor = 'public async Task UpdateTicketAsync\(int id, string title, string description, CancellationToken cancellationToken = default\)'
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
        Anchor = '\.PutAsJsonAsync\(string\.Create\(CultureInfo\.InvariantCulture, \$"/Tickets/\{id\}"\), new \{ Title = title, Description = description \}, cancellationToken\)'
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
        Anchor = 'await Api\.UpdateTicketAsync\(Id, _title, _description\);'
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
foreach ($expected in @('README.md', '.gitignore', 'local.props', '.github/workflows/ci.yml', 'build/add-module.ps1')) {
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

# ---- derived: the shape marker regions, the per-shape lines, and the eager-load fallback --------
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

Convert-TemplateMarkers -Root $moduleStaging -TemplateName 'mmca-module'
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

Write-Host "Guards passed: no surviving markers, no 'comment' / 'title' / 'opened' prose hazards, copyOnly declared for .editorconfig, both razor wrappers and $wireUpLeaf, and $wireUpLeaf names no seed noun."
Write-Host "Staging complete."
