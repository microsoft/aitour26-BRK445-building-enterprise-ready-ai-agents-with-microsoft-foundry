# Azure Infrastructure Deployment Script

## Overview

This Python script automates the deployment of Azure infrastructure resources required for the BRK445 project using **Bicep Infrastructure as Code**. It creates a complete environment including Resource Group, Application Insights, Storage Account, Azure AI Foundry Hub and Project, Azure SQL Server, and Database with initialization.

## Features

- **Bicep Infrastructure as Code**: All resources deployed via declarative Bicep template for reliability and repeatability
- **Interactive Prompts**: Guides you through the deployment with user-friendly prompts
- **Auto-generated Passwords**: Optionally generates secure SQL passwords automatically
- **Subscription Verification**: Confirms you're deploying to the correct Azure subscription
- **Live Deployment Progress**: Shows real-time Bicep deployment progress in console
- **Automatic Credential Saving**: Saves all secrets and connection strings to files (JSON + Text)
- **Complete Resource Creation**:
  - Resource Group
  - Application Insights (Web Application type)
  - Storage Account (Standard_LRS)
  - Microsoft Foundry (AI Services - unified AI capabilities)
  - Azure SQL Server (with firewall rules)
  - Azure SQL Database (Basic tier - lowest cost)
  - Database initialization with schema and seed data

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

5. **Permissions**: You need contributor access to the Azure subscription

## Usage

### Navigate to the infra folder

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

The Bicep template creates the following Azure resources:

| Resource Type | Naming Convention | Configuration |
|--------------|-------------------|---------------|
| Resource Group | `{resource_name}-rg` | Location specified by user |
| Application Insights | `{resource_name}-appinsights` | Web application type, ApplicationInsights ingestion mode |
| Storage Account | `{resource_name}st` | Standard_LRS, StorageV2 |
| Microsoft Foundry | `{resource_name}-foundry` | AI Services (unified AI capabilities) |
| SQL Server | `{resource_name}-sqlserver` | With Azure services firewall rule |
| SQL Database | `{resource_name}-db` | Basic tier (lowest cost) |

**Note**: All resources are created in the same resource group for easy management and cleanup.

### Microsoft Foundry (AI Services)

Microsoft Foundry provides unified AI capabilities through a single resource:

- **Single AI Services Account**: No separate hub/project resources
- **Unified Endpoint**: One endpoint for all AI operations
- **No Managed Resource Groups**: Everything stays in your resource group
- **Kind**: AIServices (consolidated AI capabilities)

This replaces the older pattern of creating Azure ML Workspaces (hub/project) which created separate managed resource groups.

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
