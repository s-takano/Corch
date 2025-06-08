# Set variables
$ResourceGroup = "corch-edges-group"
$FunctionAppName = "corch-edges"


Write-Host "Granting Microsoft Graph permissions to Function App managed identity..." -ForegroundColor Green

# Get managed identity ID
Write-Host "Getting managed identity ID..."
$ManagedIdentityId = az functionapp identity show --name $FunctionAppName --resource-group $ResourceGroup --query principalId --output tsv

if ([string]::IsNullOrEmpty($ManagedIdentityId)) {
    Write-Error "Could not get managed identity ID. Make sure managed identity is enabled."
    exit 1
}

Write-Host "Managed Identity ID: $ManagedIdentityId"

# Get Microsoft Graph service principal ID
Write-Host "Getting Microsoft Graph service principal ID..."
$GraphSpId = az ad sp list --filter "appId eq '00000003-0000-0000-c000-000000000000'" --query '[0].id' --output tsv
Write-Host "Graph Service Principal ID: $GraphSpId"

# Get required permission IDs
Write-Host "Getting permission IDs..."
$SitesReadAllRoleId = az ad sp show --id $GraphSpId --query "appRoles[?value=='Sites.Read.All'].id" --output tsv
$FilesReadAllRoleId = az ad sp show --id $GraphSpId --query "appRoles[?value=='Files.Read.All'].id" --output tsv

Write-Host "Sites.Read.All Role ID: $SitesReadAllRoleId"
Write-Host "Files.Read.All Role ID: $FilesReadAllRoleId"

# Create JSON payloads
$sitesPayload = @{
    principalId = $ManagedIdentityId
    resourceId = $GraphSpId
    appRoleId = $SitesReadAllRoleId
} | ConvertTo-Json -Compress

$filesPayload = @{
    principalId = $ManagedIdentityId
    resourceId = $GraphSpId
    appRoleId = $FilesReadAllRoleId
} | ConvertTo-Json -Compress

# Grant permissions
Write-Host "Granting Sites.Read.All permission..."
$sitesPayload | Out-File -FilePath "sites_payload.json" -Encoding utf8
az rest --method POST --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$ManagedIdentityId/appRoleAssignments" --body '@sites_payload.json' --headers "Content-Type=application/json"

Write-Host "Granting Files.Read.All permission..."
$filesPayload | Out-File -FilePath "files_payload.json" -Encoding utf8
az rest --method POST --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$ManagedIdentityId/appRoleAssignments" --body '@files_payload.json' --headers "Content-Type=application/json"

# Clean up
Remove-Item "sites_payload.json", "files_payload.json" -ErrorAction SilentlyContinue

Write-Host "✅ Successfully granted Microsoft Graph permissions!" -ForegroundColor Green
Write-Host "Your Function App can now access SharePoint sites and download files."