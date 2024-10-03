<#
.SYNOPSIS
Creates the ocelot.json file from appconfig values.

.DESCRIPTION
This commands read all secrets from a keyvault, formats them so that
they can be used as env vars by the portal and push the result to a
storage account.

.PARAMETER Environment
Target KPMG Pulse Front V2 environment.

.PARAMETER WorkingFolder
Path to an existing folder. A subfolder will be created and used as a working folder
for the duration of the execution. Defaults to $Env:PIPELINE_WORKSPACE

.NOTES
This command assumes az cli is installed and already logged in.
Typically, this is done during a pipeline by using the AzureCLI@2 task.
#>
 function Set-EnvironmentVariables{
   [Diagnostics.CodeAnalysis.SuppressMessageAttribute(
     'PSUseSingularNouns','',
     Justification = 'Plural is justified here'
   )]
  [CmdletBinding(SupportsShouldProcess)]
  param (
    [Parameter()]
    [ValidatePattern('(dev|itg|rec|ppr|prd)\d{2}')]
    [String]$Environment,
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [String] $WorkingFolder = "$($Env:PIPELINE_WORKSPACE)/Module",
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [String] $Subscription
  )

  try 
  {
    $envType = $Environment.Substring(0,3)
    
    $sourceContent = Get-Content -Path $WorkingFolder/ocelot.json
    $sourceContent = $sourceContent.Replace("#{env_id}#", "$Environment").Replace("#{env}#", "$envType")

    $SourcePath = "$($WorkingFolder)/ocelot.json"
    Set-Content -Path $SourcePath -Value $sourceContent

    az account set --subscription $Subscription

    $expRg = "sacegpulse$($Environment)gtw"
    $accountName = "sacegpulsegtw$($Environment)01"    
    $AccountKey = $(az storage account keys list -g $expRg -n $accountName --query [0].value -o tsv)

    $shareName = 'desktop'
    $targetFile = 'ocelot.json'
    az storage file upload --account-name $accountName --account-key "$AccountKey" --path $targetFile --share-name $shareName --source $SourcePath
  }
  finally
  {
    Remove-Item -Path $SourcePath -Recurse -Force
  }
}