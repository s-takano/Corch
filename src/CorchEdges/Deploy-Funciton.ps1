# Configuration - REPLACE THESE VALUES
$ResourceGroup = "corch-edges-group"
$FunctionAppName = "corch-edges"
$ProjectDir = (Get-Location).Path  # Current directory, change if needed

# Function to write colored output
function Write-ColorOutput($ForegroundColor) {
    $fc = $host.UI.RawUI.ForegroundColor
    $host.UI.RawUI.ForegroundColor = $ForegroundColor
    if ($args) {
        Write-Output $args
    }
    else {
        $input | Write-Output
    }
    $host.UI.RawUI.ForegroundColor = $fc
}

Write-ColorOutput Yellow "Starting Azure Function deployment process..."

# Check if Azure CLI is installed
try {
    $azVersion = (az --version) | Select-Object -First 1
    Write-ColorOutput Green "Azure CLI detected: $azVersion"
}
catch {
    Write-ColorOutput Red "Azure CLI is not installed. Please install it first."
    Write-Output "Visit: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli"
    exit 1
}

# Login to Azure
Write-ColorOutput Yellow "Logging in to Azure..."
az login

# Select subscription if needed
# az account set --subscription "YourSubscriptionId"

# Build the project
Write-ColorOutput Yellow "Building project..."
Set-Location $ProjectDir
dotnet build --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-ColorOutput Red "Build failed. Aborting deployment."
    exit 1
}

# Create a deployment package
Write-ColorOutput Yellow "Creating deployment package..."
$PublishDir = Join-Path $ProjectDir "bin\Release\net9.0"  # Adjust if your .NET version is different

# Check if 'publish' directory exists, if not, create it
if (-not (Test-Path (Join-Path $PublishDir "publish"))) {
    Write-ColorOutput Yellow "Publishing project..."
    Set-Location $ProjectDir
    dotnet publish --configuration Release
}

$PublishDir = Join-Path $PublishDir "publish"
Set-Location $PublishDir

Write-ColorOutput Yellow "Creating zip package..."
$ZipPath = Join-Path $ProjectDir "function.zip"

# Remove existing zip if it exists
if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}

# Create zip file
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($PublishDir, $ZipPath)

if (-not (Test-Path $ZipPath)) {
    Write-ColorOutput Red "Failed to create zip package. Aborting deployment."
    exit 1
}

Set-Location $ProjectDir

# Deploy the package to Azure
Write-ColorOutput Yellow "Deploying to Azure Function App: $FunctionAppName..."
az functionapp deployment source config-zip `
    -g $ResourceGroup `
    -n $FunctionAppName `
    --src $ZipPath

if ($LASTEXITCODE -ne 0) {
    Write-ColorOutput Red "Deployment failed."
    exit 1
}
else {
    Write-ColorOutput Green "Deployment completed successfully!"
    
    # Clean up
    Write-ColorOutput Yellow "Cleaning up..."
    Remove-Item $ZipPath -Force
    
    Write-ColorOutput Green "Your function is now live at: https://$FunctionAppName.azurewebsites.net"
}

# Optionally, check function app status
Write-ColorOutput Yellow "Checking function app status..."
az functionapp show `
    -g $ResourceGroup `
    -n $FunctionAppName `
    --query state

Write-ColorOutput Green "Deployment process completed."