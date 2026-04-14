# Azure Infrastructure Deployment

## Overview

This folder contains scripts to provision the Azure resources needed for the BRK445 project.

### Recommended: `setup_azure.py` (all-in-one)

The **`setup_azure.py`** script is the recommended way to set up the full Azure environment in a single run. It handles infrastructure, AI Services, model deployments, and .NET user secrets so you're ready to deploy agents and run Aspire immediately.

```bash
cd infra
python setup_azure.py
```

If you already deployed infrastructure with `deploy_azure_resources.py`, you can skip that phase and only provision AI + secrets:

```bash
python setup_azure.py --skip-infra
# or point at an existing deployment info file:
python setup_azure.py --from-deployment deployment_info_myproject_20260414.json
```

Run `python setup_azure.py --help` for all options, including model name overrides.

See the [What `setup_azure.py` does](#what-setup_azurepy-does) section below for details.

---

### Legacy: `deploy_azure_resources.py` (infrastructure only)

This script deploys **only the core infrastructure** via Bicep. It does **not** create AI Services or configure .NET user secrets — those steps must be done separately (manually or via `setup_azure.py --skip-infra`).

## Features

- **Bicep Infrastructure as Code**: All resources deployed via declarative Bicep template for reliability and repeatability
- **Interactive Prompts**: Guides you through the deployment with user-friendly prompts
- **Auto-generated Passwords**: Optionally generates secure SQL passwords automatically
- **Subscription Verification**: Confirms you're deploying to the correct Azure subscription
- **Live Deployment Progress**: Shows real-time Bicep deployment progress in console
- **Automatic Credential Saving**: Saves all secrets and connection strings to files (JSON + Text)
- **Infrastructure Created**:
  - Resource Group
  - Application Insights (Web Application type)
  - Storage Account (Standard_LRS)
  - Azure SQL Server (with firewall rules)
  - Azure SQL Database (Basic tier - lowest cost)
  - Database initialization with schema and seed data

> **Note:** AI Services (Microsoft Foundry) and model deployments are handled by `setup_azure.py`, not this script.

## Prerequisites

1. **Azure CLI**: Install from [https://docs.microsoft.com/en-us/cli/azure/install-azure-cli](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)

2. **Azure Account**: You must be logged in to Azure CLI

   ```bash
   az login
   ```

3. **Python 3.9+**: The script requires Python 3.9 or higher

4. **Python Dependencies**: Install required packages from the repository root

   ```bash
   pip install -r requirements.txt
   ```

5. **.NET SDK 10.x**: Required by `setup_azure.py` to configure user secrets (verify with `dotnet --version`)

6. **Permissions**: You need contributor access to the Azure subscription

## Usage

### Option A: Full setup with `setup_azure.py` (Recommended)

This is the fastest path to a working Azure environment. One script, everything configured.

```bash
cd infra
python setup_azure.py
```

The script will:

1. Confirm your Azure subscription
2. Deploy core infrastructure via Bicep (Resource Group, SQL, Storage, App Insights)
3. Create an Azure AI Services account (Microsoft Foundry)
4. Deploy chat (`gpt-5-mini`) and embedding (`text-embedding-3-small`) models
5. Configure .NET user secrets for both the agent deployer and Aspire AppHost

After it completes, you can immediately:

```bash
# Deploy agents to Foundry
cd infra && dotnet run

# Run the demo
cd src && aspire run
```

#### `setup_azure.py` options

| Flag | Description |
|------|-------------|
| `--skip-infra` | Skip infrastructure deployment, auto-detect existing deployment info |
| `--from-deployment FILE` | Use a specific deployment info JSON file |
| `--chat-model NAME` | Override the chat model name (default: `gpt-5-mini`) |
| `--embedding-model NAME` | Override the embedding model name (default: `text-embedding-3-small`) |
| `--chat-sku SKU` | Chat model SKU type (default: `GlobalStandard`) |
| `--embedding-sku SKU` | Embedding model SKU type (default: `Standard`) |
| `--sku-capacity N` | Model capacity in K TPM (default: `10`) |

### Option B: Infrastructure only with `deploy_azure_resources.py`

If you only need the core infrastructure (SQL, Storage, App Insights) and will set up AI Services separately:

#### Navigate to the infra folder

```bash
cd infra
```

### Run the script

```bash
python deploy_azure_resources.py
```

Or if the script is executable:

```bash
./deploy_azure_resources.py
```

### The script will prompt you for

1. **Subscription Confirmation**: Verify the current Azure subscription
2. **Resource Name**: A prefix for all resources (e.g., "brk445-demo")
3. **Azure Region**: The location for resources (default: "eastus2")
4. **SQL Admin Username**: Administrator username for SQL Server (default: "sqladmin")
5. **SQL Admin Password**: Administrator password (min 8 characters) - **Press Enter to auto-generate**
6. **Final Confirmation**: Review and confirm before deployment

### Example Interaction

```
============================================================
Current Azure Subscription:
============================================================
Name: My Azure Subscription
ID: xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx
Tenant ID: yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy
============================================================

Do you want to use this subscription? (yes/no): yes

============================================================
Resource Configuration
============================================================
Enter resource name (will be used as prefix) [brk445-demo]: brk445-06
Enter Azure region [eastus2]: 

============================================================
SQL Server Credentials
============================================================
Enter SQL Server admin username [sqladmin]: 
Enter SQL Server admin password (min 8 characters) [press Enter to auto-generate]: 
✅ Generated secure password: xY9#mK2$pL4@wQ7z
   (This will be saved to output files)

============================================================
Deployment Summary
============================================================
Resource Group: brk445-06-rg
Location: eastus2
Resource Prefix: brk445-06
SQL Admin User: sqladmin
============================================================

Proceed with deployment? (yes/no): yes
```

## Deployment Process

The script performs the following steps:

1. **Validates Azure CLI login** and confirms subscription
2. **Collects deployment parameters** (resource name, location, SQL credentials)
3. **Auto-generates secure password** if not provided
4. **Creates Resource Group**
5. **Deploys Bicep template** with live progress output:
   - Application Insights
   - Storage Account
   - SQL Server with firewall rules
   - SQL Database
   - Microsoft Foundry (AI Services)
6. **Adds client IP to SQL firewall** for immediate access
7. **Initializes database** with schema and seed data
8. **Saves deployment information** to timestamped files

## Output Files

After successful deployment, two files are created in the current directory:

### JSON File: `deployment_info_{resource_name}_{timestamp}.json`

Complete deployment information for programmatic access:

```json
{
  "timestamp": "2025-12-07T10:30:00",
  "resourceGroup": "brk445-06-rg",
  "location": "eastus2",
  "resources": {
    "applicationInsights": {
      "name": "brk445-06-appinsights",
      "instrumentationKey": "...",
      "connectionString": "..."
    },
    "sqlServer": {
      "adminUsername": "sqladmin",
      "adminPassword": "xY9#mK2$pL4@wQ7z",
      "connectionString": "Server=tcp:..."
    }
  }
}
```

### Text File: `deployment_info_{resource_name}_{timestamp}.txt`

Human-readable format with all secrets and connection strings for easy reference.

## Resources Created

### By `deploy_azure_resources.py` (Bicep template)

| Resource Type | Naming Convention | Configuration |
|--------------|-------------------|---------------|
| Resource Group | `{resource_name}-rg` | Location specified by user |
| Application Insights | `{resource_name}-appinsights` | Web application type, ApplicationInsights ingestion mode |
| Storage Account | `{resource_name}st` | Standard_LRS, StorageV2 |
| SQL Server | `{resource_name}-sqlserver` | With Azure services firewall rule |
| SQL Database | `{resource_name}-db` | Basic tier (lowest cost) |

### By `setup_azure.py` (in addition to the above)

| Resource Type | Naming Convention | Configuration |
|--------------|-------------------|---------------|
| AI Services | `{resource_name}-aiservices` | AIServices kind, S0 SKU |
| Chat Model Deployment | `gpt-5-mini` | GlobalStandard SKU (configurable) |
| Embedding Model Deployment | `text-embedding-3-small` | Standard SKU (configurable) |

**Note**: All resources are created in the same resource group for easy management and cleanup.

## What `setup_azure.py` Does

The `setup_azure.py` script performs a complete end-to-end setup in three phases:

### Phase 1: Infrastructure (Bicep)
- Creates Resource Group, App Insights, Storage Account, SQL Server, SQL Database
- Adds your client IP to the SQL firewall
- Saves deployment info to a JSON file (reusable with `--from-deployment`)

### Phase 2: AI Services (Microsoft Foundry)
- Creates an Azure AI Services account (`Microsoft.CognitiveServices/accounts`, kind `AIServices`)
- Deploys a chat model (default: `gpt-5-mini`)
- Deploys an embedding model (default: `text-embedding-3-small`)
- Retrieves the API endpoint and key

### Phase 3: .NET User Secrets
- Configures the **agent deployer** (`infra/Brk445-Console-DeployAgents.csproj`):
  - `ProjectEndpoint` — AI Services endpoint
  - `ModelDeploymentName` — chat model deployment name
  - `TenantId` — Azure tenant ID
  - `SqlServerConnectionString` — SQL connection string for database seeding
- Configures the **Aspire AppHost** (`src/ZavaAppHost/ZavaAppHost.csproj`):
  - `ConnectionStrings:microsoftfoundryproject` — AI Services endpoint
  - `ConnectionStrings:tenantId` — Azure tenant ID
  - `ConnectionStrings:appinsights` — App Insights connection string

After all three phases complete, you can immediately run `dotnet run` in `infra/` to deploy agents, and `aspire run` in `src/` to start the application.

## Database Schema

The database is initialized with the following tables and seed data:

### Tables

- **Product**: Hardware store products (15 items seeded)
- **Customer**: Customer information with tools and skills (3 customers seeded)
- **Tool**: Tool recommendations inventory (10 tools seeded)
- **Location**: Store location information (7 locations seeded)

### Seed Data

The initialization includes sample data matching the DataService project:

- 15 products (paint, tools, lumber, etc.)
- 3 customers with their owned tools and skills
- 10 tools with availability and pricing
- 7 store locations with aisle information

## Connection String

After successful deployment, the script outputs a SQL Server connection string. **Save this securely** as you'll need it to configure your applications.

Example format:

```
Server=tcp:{server_name}.database.windows.net,1433;Initial Catalog={database_name};Persist Security Info=False;User ID={admin_user};Password={admin_password};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

## Troubleshooting

### Azure CLI Not Found

```text
❌ Azure CLI is not installed. Please install it first.
```

**Solution**: Install Azure CLI from the link provided in the error message.

### Not Logged In

```text
❌ You are not logged in to Azure CLI.
```

**Solution**: Run `az login` and follow the authentication prompts.

### Bicep Deployment Failed

Check the deployment progress output for specific errors. Common issues:

- Invalid resource names (must follow Azure naming conventions)
- Region doesn't support specific services (try different region)
- Quota limits reached in subscription
- Insufficient permissions

### Database Initialization Issues

If the automatic database initialization fails, the script will provide you with the SQL script location. You can manually execute it using:

- Azure Data Studio
- SQL Server Management Studio
- Azure Portal Query Editor

### Password Requirements

SQL Server passwords must:

- Be at least 8 characters long
- Contain characters from three of the following categories:
  - Uppercase letters
  - Lowercase letters
  - Numbers
  - Non-alphanumeric characters

The auto-generated passwords always meet these requirements.

## Security Notes

1. **Firewall Rules**: The script automatically detects and adds your current client IP to the SQL Server firewall rules for secure access. Azure services are also allowed for internal connectivity.
2. **Password Security**: SQL Server passwords are passed securely via temporary files and saved to local output files. Keep these files secure!
3. **Credentials**: Store SQL credentials securely (Azure Key Vault, environment variables, etc.)
4. **Output Files**: The deployment info files contain sensitive information. Add them to `.gitignore`:

   ```text
   deployment_info_*.json
   deployment_info_*.txt
   ```

5. **Connection Strings**: Never commit connection strings to source control
6. **Basic Tier**: The Basic tier is suitable for development. Consider upgrading for production workloads.

## Cost Considerations

This deployment uses the lowest-cost tiers:

- **SQL Database**: Basic tier (approximately $5/month)
- **Storage Account**: Standard_LRS (pay-as-you-go)
- **Application Insights**: Pay-as-you-go based on data ingestion
- **AI Foundry**: Pay-as-you-go based on usage

To avoid ongoing charges, delete the resource group when no longer needed:

```bash
az group delete --name {resource_name}-rg --yes
```

## Integration with DataService

The database schema and seed data match the DataService project's initialization:

- File: `src/DataService/Models/DbInitializer.cs`
- File: `src/DataService/Models/Context.cs`

To configure the DataService to use this database, update the connection string in:

- `appsettings.json` or `appsettings.Development.json`
- Or configure via Aspire App Host connection strings

## Support

For issues or questions:

- Check the [BRK445 repository](https://github.com/elbruno/brk445-wip)
- Refer to [Azure SQL Database documentation](https://docs.microsoft.com/en-us/azure/azure-sql/)
- Review [Azure CLI documentation](https://docs.microsoft.com/en-us/cli/azure/)
