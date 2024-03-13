#Get functions definition files.
$Scripts  = @( Get-ChildItem -Path $PSScriptRoot\Public\*.ps1 -ErrorAction SilentlyContinue -Recurse )

#Dot source the files
Foreach ($import in @($Scripts))
{
  Try
  {
    . $import.fullname
  }
  Catch
  {
    Write-Error -Message "Failed to import function $($import.fullname): $_"
  }
}

# Here I might...
# Read in or create an initial config file and variable
# Export functions ($Scripts.BaseName) for WIP modules
# Set variables visible to the module and its functions only
# Export aliases to functions

foreach ($exportedFunction in $Scripts)
{
  Export-ModuleMember -Function $exportedFunction.Basename
}

az config set extension.use_dynamic_install=yes_without_prompt --only-show-errors
