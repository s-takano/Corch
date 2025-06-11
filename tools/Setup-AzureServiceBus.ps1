# Check if Azure CLI is installed
try {
    az --version | Out-Null
    Write-Host "✅ Azure CLI is installed" -ForegroundColor Green
} catch {
    Write-Host "❌ Azure CLI is not installed. Please install it first:" -ForegroundColor Red
    Write-Host "   Download from: https://aka.ms/installazurecliwindows"
    Write-Host "   Or use: winget install Microsoft.AzureCLI"
    exit 1
}

# Login to Azure
Write-Host "Logging into Azure..." -ForegroundColor Yellow
az login

# Get and set subscription
$Subscriptions = az account list --query "[].{Name:name, Id:id, State:state}" --output json | ConvertFrom-Json
if ($Subscriptions.Count -gt 1) {
    Write-Host "Available subscriptions:" -ForegroundColor Cyan
    $Subscriptions | Format-Table Name, Id, State
    
    $SubscriptionId = Read-Host "Enter your subscription ID"
} else {
    $SubscriptionId = $Subscriptions[0].Id
    Write-Host "Using subscription: $($Subscriptions[0].Name)" -ForegroundColor Green
}

az account set --subscription $SubscriptionId

# Set variables (customize these)
# Environment selection
Write-Host ""
Write-Host "Select environment:" -ForegroundColor Cyan
Write-Host "1. dev" -ForegroundColor White
Write-Host "2. test" -ForegroundColor White
Write-Host "3. staging" -ForegroundColor White
Write-Host "4. prod" -ForegroundColor White

do {
    $EnvironmentChoice = Read-Host "Enter your choice (1-4)"
    switch ($EnvironmentChoice) {
        "1" { $Environment = "dev"; break }
        "2" { $Environment = "test"; break }
        "3" { $Environment = "staging"; break }
        "4" { $Environment = "prod"; break }
        default { 
            Write-Host "❌ Invalid choice. Please enter 1, 2, 3, or 4." -ForegroundColor Red
            $Environment = $null
        }
    }
} while ($null -eq $Environment)

Write-Host "Selected environment: $Environment" -ForegroundColor Green

$Location = "Japan East"
$ResourceGroup = "corch-edges-env"
$ProjectName = "corch-edges"

# -------------------------------
# Create or reuse resource group
Write-Host "Checking for existing resource group '$ResourceGroup'..." -ForegroundColor Yellow

$ExistingRg = az group show --name $ResourceGroup --query name --output tsv 2>$null

if ($ExistingRg) {
    Write-Host "✅ Resource Group '$ResourceGroup' already exists, reusing it" -ForegroundColor Green
} else {
    Write-Host "Creating new resource group '$ResourceGroup'..." -ForegroundColor Yellow
    
    az group create `
        --name $ResourceGroup `
        --location $Location `
        --tags Environment=$Environment Project=$ProjectName

    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Resource Group '$ResourceGroup' created" -ForegroundColor Green
    } else {
        Write-Host "❌ Failed to create Resource Group" -ForegroundColor Red
        exit 1
    }
}

# -------------------------------
# Create Service Bus namespace
$ServiceBusNamespace = "$ProjectName-$Environment-sb1"

Write-Host "Creating Service Bus namespace '$ServiceBusNamespace'..." -ForegroundColor Yellow

az servicebus namespace create `
    --name $ServiceBusNamespace `
    --resource-group $ResourceGroup `
    --location $Location `
    --sku Basic `
    --tags Environment=$Environment Project=$ProjectName

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Service Bus namespace '$ServiceBusNamespace' created" -ForegroundColor Green
} else {
    Write-Host "❌ Failed to create Service Bus namespace" -ForegroundColor Red
    exit 1
}

# Create queues
$Queues = @("sp-changes", "sp-changes-integration-test", "sp-changes-prod", "deadletter-queue")

foreach ($Queue in $Queues) {
    Write-Host "Creating queue '$Queue'..." -ForegroundColor Yellow
    
    az servicebus queue create `
        --resource-group $ResourceGroup `
        --namespace-name $ServiceBusNamespace `
        --name $Queue `
        --max-size 1024 `
        --default-message-time-to-live "P14D" `
        --lock-duration "PT1M" `
        --max-delivery-count 10 `
        --enable-dead-lettering-on-message-expiration true
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Queue '$Queue' created" -ForegroundColor Green
    } else {
        Write-Host "❌ Failed to create queue '$Queue'" -ForegroundColor Red
    }
}

# Create Service Bus access policies
Write-Host "Creating Service Bus access policies..." -ForegroundColor Yellow

# Full access policy for admins/tests
az servicebus namespace authorization-rule create `
    --resource-group $ResourceGroup `
    --namespace-name $ServiceBusNamespace `
    --name "FullAccessPolicy" `
    --rights Manage Send Listen

# Send-only policy for producers
az servicebus namespace authorization-rule create `
    --resource-group $ResourceGroup `
    --namespace-name $ServiceBusNamespace `
    --name "SendOnlyPolicy" `
    --rights Send

# Listen-only policy for consumers
az servicebus namespace authorization-rule create `
    --resource-group $ResourceGroup `
    --namespace-name $ServiceBusNamespace `
    --name "ListenOnlyPolicy" `
    --rights Listen

Write-Host "✅ Service Bus access policies created" -ForegroundColor Green

# --------------------------------------
# Create Key Vault with conflict resolution
$KeyVaultName = "$ProjectName-$Environment-kv"

Write-Host "Checking for existing Key Vault '$KeyVaultName'..." -ForegroundColor Yellow

# Check if Key Vault already exists (active)
$ExistingKv = az keyvault show --name $KeyVaultName --query name --output tsv 2>$null

if ($ExistingKv) {
    Write-Host "✅ Key Vault '$KeyVaultName' already exists and is active" -ForegroundColor Green
} else {
    # Check if Key Vault exists in soft-deleted state
    Write-Host "Checking for soft-deleted Key Vault..." -ForegroundColor Yellow
    $DeletedKv = az keyvault list-deleted --query "[?name=='$KeyVaultName'].name" --output tsv 2>$null
    
    if ($DeletedKv) {
        Write-Host "⚠️ Found soft-deleted Key Vault '$KeyVaultName'" -ForegroundColor Yellow
        Write-Host "Recovering soft-deleted Key Vault..." -ForegroundColor Yellow
        
        az keyvault recover --name $KeyVaultName --location $Location
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Key Vault '$KeyVaultName' recovered from soft-delete" -ForegroundColor Green
        } else {
            Write-Host "❌ Failed to recover Key Vault" -ForegroundColor Red
            Write-Host "Attempting to purge and recreate..." -ForegroundColor Yellow
            
            # Purge the soft-deleted Key Vault
            az keyvault purge --name $KeyVaultName --location $Location
            Start-Sleep -Seconds 30
            
            # Create new Key Vault
            az keyvault create `
                --name $KeyVaultName `
                --resource-group $ResourceGroup `
                --location $Location `
                --enable-rbac-authorization `
                --tags Environment=$Environment Project=$ProjectName
                
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ Key Vault '$KeyVaultName' created after purge" -ForegroundColor Green
            } else {
                Write-Host "❌ Failed to create Key Vault after purge" -ForegroundColor Red
                exit 1
            }
        }
    } else {
        # No existing Key Vault, create new one
        Write-Host "Creating new Key Vault '$KeyVaultName'..." -ForegroundColor Yellow
        
        az keyvault create `
            --name $KeyVaultName `
            --resource-group $ResourceGroup `
            --location $Location `
            --enable-rbac-authorization `
            --tags Environment=$Environment Project=$ProjectName
            
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Key Vault '$KeyVaultName' created" -ForegroundColor Green
        } else {
            Write-Host "❌ Failed to create Key Vault" -ForegroundColor Red
            exit 1
        }
    }
}

# Get current user info and Key Vault ID
$CurrentUser = az ad signed-in-user show --query userPrincipalName --output tsv
$KeyVaultId = az keyvault show --name $KeyVaultName --query id --output tsv

# Assign Key Vault Administrator role to current user
Write-Host "Assigning Key Vault Administrator role to $CurrentUser..." -ForegroundColor Yellow

az role assignment create `
    --assignee $CurrentUser `
    --role "Key Vault Administrator" `
    --scope $KeyVaultId

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Key Vault Administrator role assigned to $CurrentUser" -ForegroundColor Green
} else {
    Write-Host "❌ Failed to assign Key Vault role" -ForegroundColor Red
}

# Wait for role propagation
Write-Host "⏳ Waiting 30 seconds for role propagation..." -ForegroundColor Yellow
Start-Sleep -Seconds 30

#------------------------
Write-Host "Storing Service Bus connection strings in Key Vault..." -ForegroundColor Yellow

# Get connection strings for different policies
$FullAccessConn = az servicebus namespace authorization-rule keys list `
    --resource-group $ResourceGroup `
    --namespace-name $ServiceBusNamespace `
    --name "FullAccessPolicy" `
    --query primaryConnectionString --output tsv

$SendOnlyConn = az servicebus namespace authorization-rule keys list `
    --resource-group $ResourceGroup `
    --namespace-name $ServiceBusNamespace `
    --name "SendOnlyPolicy" `
    --query primaryConnectionString --output tsv

$ListenOnlyConn = az servicebus namespace authorization-rule keys list `
    --resource-group $ResourceGroup `
    --namespace-name $ServiceBusNamespace `
    --name "ListenOnlyPolicy" `
    --query primaryConnectionString --output tsv

# Store connection strings in Key Vault
az keyvault secret set `
    --vault-name $KeyVaultName `
    --name "ServiceBusConnection" `
    --value $FullAccessConn `
    --tags Environment=$Environment Purpose=FullAccess

az keyvault secret set `
    --vault-name $KeyVaultName `
    --name "ServiceBusConnection-SendOnly" `
    --value $SendOnlyConn `
    --tags Environment=$Environment Purpose=SendOnly

az keyvault secret set `
    --vault-name $KeyVaultName `
    --name "ServiceBusConnection-ListenOnly" `
    --value $ListenOnlyConn `
    --tags Environment=$Environment Purpose=ListenOnly

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Service Bus connection strings stored in Key Vault" -ForegroundColor Green
} else {
    Write-Host "❌ Failed to store connection strings in Key Vault" -ForegroundColor Red
}

#------------------------------------------------------------------------
Write-Host "Creating Service Principal for CI/CD..." -ForegroundColor Yellow

# Create service principal
$ServicePrincipalName = "$ProjectName-$Environment-sp"
$ResourceGroupScope = "/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup"

$SpInfo = az ad sp create-for-rbac `
    --name $ServicePrincipalName `
    --role "Contributor" `
    --scopes $ResourceGroupScope `
    --sdk-auth | ConvertFrom-Json

# Extract service principal details
$SpAppId = $SpInfo.clientId
$SpPassword = $SpInfo.clientSecret
$SpTenant = $SpInfo.tenantId

# Assign Key Vault Secrets Officer role to service principal
az role assignment create `
    --assignee $SpAppId `
    --role "Key Vault Secrets Officer" `
    --scope $KeyVaultId

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Service Principal '$ServicePrincipalName' created" -ForegroundColor Green
    Write-Host "📋 Store these values securely for CI/CD:" -ForegroundColor Cyan
    Write-Host "Client ID: $SpAppId" -ForegroundColor White
    Write-Host "Client Secret: $SpPassword" -ForegroundColor White
    Write-Host "Tenant ID: $SpTenant" -ForegroundColor White
} else {
    Write-Host "❌ Failed to create Service Principal" -ForegroundColor Red
}

# Final summary
Write-Host ""
Write-Host "🎉 Azure resources created successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "📊 Resource Summary:" -ForegroundColor Cyan
Write-Host "==================="
Write-Host "Resource Group: $ResourceGroup"
Write-Host "Service Bus Namespace: $ServiceBusNamespace"
Write-Host "Key Vault: $KeyVaultName"
Write-Host "Service Principal: $ServicePrincipalName"
Write-Host ""
Write-Host "🔗 Connection Information:" -ForegroundColor Cyan
Write-Host "=========================="
Write-Host "Service Bus Full Access: stored in Key Vault as 'ServiceBusConnection'"
Write-Host "Service Bus Send Only: stored in Key Vault as 'ServiceBusConnection-SendOnly'"
Write-Host "Service Bus Listen Only: stored in Key Vault as 'ServiceBusConnection-ListenOnly'"
Write-Host ""
Write-Host "🔑 Key Vault URLs:" -ForegroundColor Cyan
Write-Host "=================="
Write-Host "Key Vault URL: https://$KeyVaultName.vault.azure.net/"
Write-Host ""
Write-Host "📝 Next Steps:" -ForegroundColor Cyan
Write-Host "=============="
Write-Host "1. Update your .env file with: ConnectionStrings__ServiceBusConnection=@Microsoft.KeyVault(VaultName=$KeyVaultName;SecretName=ServiceBusConnection)"
Write-Host "2. Configure your CI/CD with the Service Principal credentials"
Write-Host "3. Run your integration tests: dotnet test --filter 'ServiceBus'"

# Test the setup
Write-Host ""
Write-Host "🧪 Testing the setup..." -ForegroundColor Cyan

# Test Service Bus connectivity
Write-Host "Testing Service Bus..." -ForegroundColor Yellow
az servicebus queue list `
    --resource-group $ResourceGroup `
    --namespace-name $ServiceBusNamespace `
    --output table

# Test Key Vault access
Write-Host "Testing Key Vault access..." -ForegroundColor Yellow
az keyvault secret list `
    --vault-name $KeyVaultName `
    --output table

# Display connection string for verification
Write-Host "Service Bus Connection String (first 50 chars):" -ForegroundColor Yellow
$ConnectionString = az keyvault secret show `
    --vault-name $KeyVaultName `
    --name "ServiceBusConnection" `
    --query "value" --output tsv

if ($ConnectionString) {
    Write-Host $ConnectionString.Substring(0, [Math]::Min(50, $ConnectionString.Length)) -ForegroundColor White
    Write-Host "✅ Setup completed successfully!" -ForegroundColor Green
} else {
    Write-Host "❌ Could not retrieve connection string" -ForegroundColor Red
}