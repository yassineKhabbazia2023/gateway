<#
.SYNOPSIS
Merges the per-domain ocelot.*.json files into a single ocelot.json.

.DESCRIPTION
The gateway routing configuration is maintained in the repository as one file
per functional domain (src/Config/ocelot.<domain>.json, domain = first upstream
path segment after /gtw/), plus:
  - ocelot.global.json     -> GlobalConfiguration (required, exactly one file)
  - ocelot.aggregates.json -> Aggregates
  - ocelot.swagger.json    -> SwaggerEndPoints (MMLib.SwaggerForOcelot)

This command concatenates the sections of every ocelot.*.json file (ordinal
file-name order, relative route order preserved within each file) and produces
the single ocelot.json expected by the deployment (Set-EnvironmentVariables)
and by the gateway at runtime (OCELOT_CONFIG_PATH).

Upstream templates are partitioned by domain, so the concatenation order across
files has no impact on Ocelot route matching.

.PARAMETER SourceFolder
Folder containing the ocelot.*.json files (typically src/Config).

.PARAMETER OutputFile
Full path of the merged ocelot.json to produce.
#>
function Merge-OcelotConfig {
  [CmdletBinding()]
  param (
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [String] $SourceFolder,
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [String] $OutputFile
  )

  $files = @(Get-ChildItem -Path $SourceFolder -Filter 'ocelot.*.json' -File)
  if ($files.Count -eq 0) {
    throw "No ocelot.*.json file found in '$SourceFolder'."
  }

  # Ordinal sort for a deterministic merge order on every agent/culture.
  $sortedNames = [String[]]($files.Name)
  [Array]::Sort($sortedNames, [System.StringComparer]::Ordinal)

  $routes = [System.Collections.Generic.List[object]]::new()
  $aggregates = [System.Collections.Generic.List[object]]::new()
  $swaggerEndPoints = [System.Collections.Generic.List[object]]::new()
  $globalConfiguration = $null

  foreach ($name in $sortedNames) {
    $config = Get-Content -Path (Join-Path $SourceFolder $name) -Raw | ConvertFrom-Json

    foreach ($route in @($config.Routes)) {
      if ($null -ne $route) { $routes.Add($route) }
    }
    foreach ($aggregate in @($config.Aggregates)) {
      if ($null -ne $aggregate) { $aggregates.Add($aggregate) }
    }
    foreach ($endpoint in @($config.SwaggerEndPoints)) {
      if ($null -ne $endpoint) { $swaggerEndPoints.Add($endpoint) }
    }
    if ($null -ne $config.GlobalConfiguration) {
      if ($null -ne $globalConfiguration) {
        throw "GlobalConfiguration is defined in more than one file (second occurrence: '$name')."
      }
      $globalConfiguration = $config.GlobalConfiguration
    }
  }

  if ($null -eq $globalConfiguration) {
    throw "GlobalConfiguration not found. It must be defined in ocelot.global.json."
  }
  if ($routes.Count -eq 0) {
    throw 'The merged configuration contains no route.'
  }

  $merged = [ordered]@{
    Routes              = $routes
    Aggregates          = $aggregates
    SwaggerEndPoints    = $swaggerEndPoints
    GlobalConfiguration = $globalConfiguration
  }

  $merged | ConvertTo-Json -Depth 64 | Set-Content -Path $OutputFile
  Write-Host "Merged $($files.Count) files ($($routes.Count) routes, $($aggregates.Count) aggregates, $($swaggerEndPoints.Count) swagger endpoints) into '$OutputFile'."
}
