#!/usr/bin/env python3
"""
Azure Infrastructure Deployment Script using Bicep
This script creates Azure resources using Bicep template and initializes the SQL database.
"""

import subprocess
import json
import sys
import os
import tempfile
import secrets
import string
from typing import Optional, Dict, Any
from pathlib import Path
from datetime import datetime


def run_command(command: str, capture_output: bool = True, check: bool = True) -> Optional[str]:
    """Run a shell command and return its output."""
    try:
        result = subprocess.run(
            command,
            shell=True,
            check=check,
            capture_output=True,
            text=True
        )
        # Always capture output, but optionally print it
        if not capture_output:
            # Print output for visibility even though we're capturing it
            if result.stdout:
                print(result.stdout)
            if result.stderr:
                print(result.stderr, file=sys.stderr)
        return result.stdout.strip()
    except subprocess.CalledProcessError as e:
        print(f"Error executing command: {command}")
        if e.stderr:
            print(f"Error: {e.stderr}")
        if e.stdout:
            print(f"Output: {e.stdout}")
        if check:
            sys.exit(1)
        return None


def check_az_login() -> bool:
    """Check if user is logged in to Azure CLI."""
    result = run_command("az account show", check=False)
    return result is not None


def get_current_subscription() -> Dict[str, Any]:
    """Get the current Azure subscription details."""
    output = run_command("az account show")
    if output:
        return json.loads(output)
    return {}


def confirm_subscription(subscription: Dict[str, Any]) -> bool:
    """Ask user to confirm the current subscription."""
    print("\n" + "=" * 60)
    print("Current Azure Subscription:")
    print("=" * 60)
    print(f"Name: {subscription.get('name', 'N/A')}")
    print(f"ID: {subscription.get('id', 'N/A')}")
    print(f"Tenant ID: {subscription.get('tenantId', 'N/A')}")
    print("=" * 60)
    
    response = input("\nDo you want to use this subscription? (yes/no): ").strip().lower()
    return response in ['yes', 'y']


def get_user_input(prompt: str, default: str = None) -> str:
    """Get input from user with optional default value."""
    if default:
        user_input = input(f"{prompt} [{default}]: ").strip()
        return user_input if user_input else default
    else:
        user_input = ""
        while not user_input:
            user_input = input(f"{prompt}: ").strip()
        return user_input


def generate_secure_password(length: int = 16) -> str:
    """Generate a secure random password."""
    # Password must contain: uppercase, lowercase, digit, and special character
    alphabet = string.ascii_letters + string.digits + "!@#$%^&*()-_=+[]{}|;:,.<>?"
    
    while True:
        password = ''.join(secrets.choice(alphabet) for i in range(length))
        # Ensure it meets complexity requirements
        if (any(c.islower() for c in password)
            and any(c.isupper() for c in password)
            and any(c.isdigit() for c in password)
            and any(c in "!@#$%^&*()-_=+[]{}|;:,.<>?" for c in password)):
            return password


def save_deployment_info(resource_group: str, location: str, resource_name: str, 
                        admin_user: str, admin_password: str, 
                        deployment_outputs: Dict[str, Any]) -> None:
    """Save deployment information to text and JSON files."""
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    base_filename = f"deployment_info_{resource_name}_{timestamp}"
    
    # Prepare deployment info
    connection_string = get_connection_string(
        deployment_outputs.get('sqlServerName'),
        deployment_outputs.get('sqlDatabaseName'),
        admin_user,
        admin_password
    )
    
    deployment_info = {
        "timestamp": datetime.now().isoformat(),
        "resourceGroup": resource_group,
        "location": location,
        "resourcePrefix": resource_name,
        "resources": {
            "applicationInsights": {
                "name": deployment_outputs.get('appInsightsName'),
                "instrumentationKey": deployment_outputs.get('appInsightsInstrumentationKey'),
                "connectionString": deployment_outputs.get('appInsightsConnectionString')
            },
            "storageAccount": {
                "name": deployment_outputs.get('storageAccountName')
            },
            "sqlServer": {
                "name": deployment_outputs.get('sqlServerName'),
                "fqdn": deployment_outputs.get('sqlServerFqdn'),
                "adminUsername": admin_user,
                "adminPassword": admin_password,
                "databaseName": deployment_outputs.get('sqlDatabaseName'),
                "connectionString": connection_string
            }
        }
    }
    
    # Save as JSON
    json_file = f"{base_filename}.json"
    with open(json_file, 'w', encoding='utf-8') as f:
        json.dump(deployment_info, f, indent=2)
    print(f"\n💾 Deployment info saved to: {json_file}")
    
    # Save as text
    txt_file = f"{base_filename}.txt"
    with open(txt_file, 'w', encoding='utf-8') as f:
        f.write("=" * 60 + "\n")
        f.write("Azure Infrastructure Deployment Information\n")
        f.write("=" * 60 + "\n")
        f.write(f"Timestamp: {deployment_info['timestamp']}\n")
        f.write(f"Resource Group: {resource_group}\n")
        f.write(f"Location: {location}\n")
        f.write(f"Resource Prefix: {resource_name}\n")
        f.write("\n" + "=" * 60 + "\n")
        f.write("Application Insights\n")
        f.write("=" * 60 + "\n")
        f.write(f"Name: {deployment_outputs.get('appInsightsName')}\n")
        f.write(f"Instrumentation Key: {deployment_outputs.get('appInsightsInstrumentationKey')}\n")
        f.write(f"Connection String: {deployment_outputs.get('appInsightsConnectionString')}\n")
        f.write("\n" + "=" * 60 + "\n")
        f.write("Storage Account\n")
        f.write("=" * 60 + "\n")
        f.write(f"Name: {deployment_outputs.get('storageAccountName')}\n")
        f.write("\n" + "=" * 60 + "\n")

        f.write("SQL Server\n")
        f.write("=" * 60 + "\n")
        f.write(f"Server Name: {deployment_outputs.get('sqlServerName')}\n")
        f.write(f"Server FQDN: {deployment_outputs.get('sqlServerFqdn')}\n")
        f.write(f"Admin Username: {admin_user}\n")
        f.write(f"Admin Password: {admin_password}\n")
        f.write(f"Database Name: {deployment_outputs.get('sqlDatabaseName')}\n")
        f.write(f"\nConnection String:\n{connection_string}\n")
        f.write("\n" + "=" * 60 + "\n")
        f.write("⚠️  IMPORTANT: Keep this file secure and do not commit to source control!\n")
        f.write("=" * 60 + "\n")
    
    print(f"💾 Deployment info saved to: {txt_file}")
    
    return deployment_info


def create_resource_group(resource_group: str, location: str) -> bool:
    """Create an Azure resource group."""
    print(f"\n📦 Creating resource group '{resource_group}' in '{location}'...")
    
    command = f"az group create --name {resource_group} --location {location}"
    result = run_command(command)
    
    if result:
        print(f"✅ Resource group '{resource_group}' created successfully.")
        return True
    return False


def deploy_bicep_template(resource_group: str, resource_name: str, location: str, sql_admin_user: str, sql_admin_password: str) -> Optional[Dict[str, Any]]:
    """Deploy infrastructure using Bicep template."""
    print(f"\n🚀 Deploying infrastructure using Bicep...")
    
    # Get the directory where this script is located
    script_dir = Path(__file__).parent
    bicep_file = script_dir / "main.bicep"
    
    if not bicep_file.exists():
        print(f"❌ Bicep template not found at: {bicep_file}")
        return None
    
    # Validate Bicep template first
    print(f"   Validating Bicep template...")
    validate_command = f'az bicep build --file "{bicep_file}" --outdir "{script_dir}"'
    validate_result = run_command(validate_command, check=False)
    
    if validate_result is None or "error" in validate_result.lower():
        print(f"   ❌ Bicep validation failed:")
        if validate_result:
            print(validate_result)
        return None
    
    print(f"   ✅ Bicep template is valid")
    
    # Create parameters file content
    parameters = {
        "resourcePrefix": {"value": resource_name},
        "location": {"value": location},
        "sqlAdminUsername": {"value": sql_admin_user},
        "sqlAdminPassword": {"value": sql_admin_password}
    }
    
    # Write parameters to temp file
    with tempfile.NamedTemporaryFile(mode='w', suffix='.json', delete=False, encoding='utf-8') as param_file:
        json.dump({"$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#", "contentVersion": "1.0.0.0", "parameters": parameters}, param_file, indent=2)
        param_file_path = param_file.name
    
    try:
        # Deploy using Azure CLI
        deployment_name = f"{resource_name}-deployment"
        command = f'az deployment group create --resource-group {resource_group} --name {deployment_name} --template-file "{bicep_file}" --parameters "{param_file_path}"'
        
        print(f"   Deployment name: {deployment_name}")
        print(f"   This may take several minutes...")
        print(f"   Deployment progress:\n")
        
        # Run deployment - this will both print and return output
        result = run_command(command, capture_output=False, check=False)
        
        # Give it a moment for deployment to settle
        import time
        time.sleep(2)
        
        # Check if deployment was successful by querying the deployment
        status_command = f'az deployment group show --resource-group {resource_group} --name {deployment_name} --query "properties.provisioningState" -o json'
        status_result = run_command(status_command, check=False)
        
        if status_result:
            try:
                status = json.loads(status_result).strip('"') if isinstance(json.loads(status_result), str) else status_result
                status = status.strip('"') if isinstance(status, str) else status
            except:
                status = "Unknown"
            
            print(f"\n   Deployment status: {status}")
            
            if status == "Succeeded":
                # Get deployment outputs
                output_command = f'az deployment group show --resource-group {resource_group} --name {deployment_name} --query "properties.outputs" -o json'
                output_result = run_command(output_command, check=False)
                
                if output_result:
                    try:
                        outputs = json.loads(output_result)
                        
                        # Extract output values
                        output_values = {}
                        for key, value in outputs.items():
                            output_values[key] = value.get('value')
                        
                        print(f"\n✅ Infrastructure deployed successfully.")
                        return output_values
                    except json.JSONDecodeError as je:
                        print(f"❌ Could not parse deployment outputs: {je}")
                        print(f"Output result: {output_result}")
                        return None
                else:
                    print(f"❌ Could not retrieve deployment outputs.")
                    return None
            else:
                print(f"\n❌ Bicep deployment failed with status: {status}")
                # Try to get error details
                error_command = f'az deployment group show --resource-group {resource_group} --name {deployment_name} --query "properties.error" -o json'
                error_result = run_command(error_command, check=False)
                if error_result and error_result != "null":
                    try:
                        error_details = json.loads(error_result)
                        print(f"Error details: {json.dumps(error_details, indent=2)}")
                    except:
                        print(f"Error details: {error_result}")
                return None
        else:
            print(f"❌ Could not check deployment status.")
            # Try listing deployments to debug
            list_command = f'az deployment group list --resource-group {resource_group} --query "[?name==\'{deployment_name}\']" -o json'
            list_result = run_command(list_command, check=False)
            if list_result:
                print(f"Deployment found: {list_result}")
            return None
            
    except Exception as e:
        print(f"❌ Error deploying Bicep template: {str(e)}")
        import traceback
        traceback.print_exc()
        return None
    finally:
        # Clean up parameters file
        try:
            os.unlink(param_file_path)
        except OSError:
            pass


def initialize_database(server_name: str, database_name: str, admin_user: str, admin_password: str):
    """Initialize the database with schema and seed data."""
    print(f"   Creating schema and loading seed data...")
    
    # Create SQL script for initialization
    sql_script = """
-- Create Products table
CREATE TABLE Product (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(255) NOT NULL,
    Description NVARCHAR(MAX),
    Price DECIMAL(18,2) NOT NULL,
    ImageUrl NVARCHAR(500)
);

-- Create Customer table
CREATE TABLE Customer (
    Id NVARCHAR(50) PRIMARY KEY,
    Name NVARCHAR(255) NOT NULL,
    OwnedTools NVARCHAR(MAX),
    Skills NVARCHAR(MAX)
);

-- Create Tool table
CREATE TABLE Tool (
    Sku NVARCHAR(50) PRIMARY KEY,
    Name NVARCHAR(255) NOT NULL,
    IsAvailable BIT NOT NULL,
    Price DECIMAL(18,2) NOT NULL,
    Description NVARCHAR(MAX)
);

-- Create Location table
CREATE TABLE Location (
    Section NVARCHAR(100),
    Aisle NVARCHAR(50),
    Shelf NVARCHAR(50),
    Description NVARCHAR(MAX),
    PRIMARY KEY (Section, Aisle)
);

-- Seed Products
INSERT INTO Product (Name, Description, Price, ImageUrl) VALUES
('Interior Wall Paint - White Matte', 'Premium interior latex paint with smooth matte finish, low VOC.', 29.99, 'paint_white_1.png'),
('Exterior Wood Stain - Cedar', 'Weather-resistant wood stain for decks and siding with UV protection.', 34.99, 'wood_stain_cedar.png'),
('Cordless Drill Kit', '18V cordless drill with two batteries, charger, and 25-piece bit set.', 79.99, 'cordless_drill_18v.png'),
('Circular Saw - 7 1/4"', 'Powerful circular saw for precise cuts in plywood and dimensional lumber.', 119.99, 'circular_saw_7_14.png'),
('Plywood Sheet - 3/4 inch', 'High-quality furniture-grade plywood sheet, 4x8 ft, versatile for cabinetry and shelving.', 49.99, 'plywood_3_4_4x8.png'),
('Pressure-Treated Lumber - 2x4', '2x4 pressure-treated lumber, suitable for outdoor framing and decks.', 6.49, 'lumber_2x4.png'),
('Painter''s Roller Kit', 'Complete roller kit with roller covers, tray, and extension pole for smooth wall coverage.', 19.99, 'painters_roller_kit.png'),
('Finish Nails - Box 1000', '1 1/4 inch finish nails for trim and finishing work.', 7.99, 'finish_nails_box.png'),
('Wood Glue - 16 oz', 'High-strength PVA wood glue for furniture and cabinetry projects.', 6.99, 'wood_glue_16oz.png'),
('Sandpaper Assortment', 'Assorted grit sandpaper pack (80-400 grit) for rough and fine sanding.', 9.99, 'sandpaper_assortment.png'),
('Stud Finder', 'Electronic stud finder for locating studs, live wires, and edges behind walls.', 24.99, 'stud_finder.png'),
('Caulking Gun + Silicone', 'Smooth-action caulking gun with a tube of silicone sealant for gaps and joints.', 12.99, 'caulking_gun_silicone.png'),
('Toolbox - Metal', 'Durable metal toolbox with removable tray for organising hand tools.', 39.99, 'metal_toolbox.png'),
('Tape Measure - 25ft', '25-foot tape measure with locking mechanism and belt clip.', 9.49, 'tape_measure_25ft.png'),
('Protective Safety Glasses', 'ANSI-rated safety glasses with anti-fog coating for eye protection.', 6.49, 'safety_glasses.png');

-- Seed Customers
INSERT INTO Customer (Id, Name, OwnedTools, Skills) VALUES
('1', 'John Smith', 'hammer,screwdriver,measuring tape', 'basic DIY,painting'),
('2', 'Sarah Johnson', 'drill,saw,level,hammer', 'intermediate DIY,woodworking,tiling'),
('3', 'Mike Davis', 'basic toolkit', 'beginner DIY');

-- Seed Tools
INSERT INTO Tool (Name, Sku, IsAvailable, Price, Description) VALUES
('Paint Roller', 'PAINT-ROLLER-9IN', 1, 12.99, '9-inch paint roller for smooth walls'),
('Paint Brush Set', 'BRUSH-SET-3PC', 1, 24.99, '3-piece brush set for detail work'),
('Drop Cloth', 'DROP-CLOTH-9X12', 1, 8.99, 'Plastic drop cloth protection'),
('Circular Saw', 'SAW-CIRCULAR-7IN', 1, 89.99, '7.25-inch circular saw for wood cutting'),
('Wood Stain', 'STAIN-WOOD-QT', 0, 15.99, '1-quart wood stain in natural color'),
('Safety Glasses', 'SAFETY-GLASSES', 1, 5.99, 'Safety glasses for eye protection'),
('Work Gloves', 'GLOVES-WORK-L', 1, 7.99, 'Heavy-duty work gloves'),
('Cordless Drill', 'DRILL-CORDLESS', 1, 79.99, '18V cordless drill with battery'),
('Level', 'LEVEL-2FT', 1, 19.99, '2-foot aluminum level'),
('Tile Cutter', 'TILE-CUTTER', 0, 45.99, 'Manual tile cutting tool');

-- Seed Locations
INSERT INTO Location (Section, Aisle, Shelf, Description) VALUES
('Hardware Tools', 'A1', 'Middle', 'Hand and power tools section'),
('Paint & Supplies', 'B3', 'Top', 'Paint and painting supplies'),
('Garden Center', 'Outside', 'Ground Level', 'Outdoor garden section'),
('General Merchandise', 'C2', 'Middle', 'General merchandise'),
('Lumber & Building Materials', 'D1', 'Ground Level', 'Lumber and building materials'),
('Electrical', 'E2', 'Middle', 'Electrical supplies and fixtures'),
('Plumbing', 'F1', 'Bottom', 'Plumbing supplies and fixtures');
"""
    
    # Write SQL script to temporary file
    with tempfile.NamedTemporaryFile(mode='w', suffix='.sql', delete=False) as f:
        f.write(sql_script)
        sql_file = f.name
    
    try:
        # Execute SQL script using sqlcmd via Azure CLI
        print("   Creating database tables...")
        
        # Write password to temporary file for secure passing
        with tempfile.NamedTemporaryFile(mode='w', suffix='.txt', delete=False) as pwd_file:
            pwd_file.write(admin_password)
            pwd_file_path = pwd_file.name
        
        try:
            command = f"az sql db execute --name {database_name} --server {server_name} --admin-user {admin_user} --admin-password $(cat {pwd_file_path}) --file {sql_file}"
            print("   Executing SQL script...")
            result = run_command(command, check=False)
            
            if result is not None:
                print(f"   ✅ Database schema created successfully")
                print(f"   Seeding tables with sample data...")
                print(f"   ✅ Database initialized successfully with schema and seed data.")
            else:
                print(f"   ⚠️  Note: Database schema and seed data initialization may need to be done manually.")
                print(f"   SQL script saved to: {sql_file}")
                print(f"   You can execute it using Azure Data Studio or SQL Server Management Studio.")
        finally:
            # Clean up password file
            try:
                os.unlink(pwd_file_path)
            except OSError:
                pass
    except Exception as e:
        print(f"   ⚠️  Could not initialize database automatically: {str(e)}")
        print(f"   SQL script saved to: {sql_file}")
        print(f"   You can execute it manually using Azure Data Studio or SQL Server Management Studio.")
    finally:
        # Clean up temp file
        try:
            os.unlink(sql_file)
        except (OSError, FileNotFoundError):
            pass


def get_connection_string(server_name: str, database_name: str, admin_user: str, admin_password: str) -> str:
    """Generate SQL Server connection string."""
    return f"Server=tcp:{server_name}.database.windows.net,1433;Initial Catalog={database_name};Persist Security Info=False;User ID={admin_user};Password={admin_password};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"


def main():
    """Main function to orchestrate the deployment."""
    print("=" * 60)
    print("Azure Infrastructure Deployment Script")
    print("=" * 60)
    
    # Check if Azure CLI is installed
    if run_command("az --version", check=False) is None:
        print("❌ Azure CLI is not installed. Please install it first.")
        print("   Visit: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli")
        sys.exit(1)
    
    # Check if user is logged in
    if not check_az_login():
        print("❌ You are not logged in to Azure CLI.")
        print("   Please run: az login")
        sys.exit(1)
    
    # Get and confirm current subscription
    subscription = get_current_subscription()
    if not subscription:
        print("❌ Could not retrieve current subscription.")
        sys.exit(1)
    
    if not confirm_subscription(subscription):
        print("\n❌ Deployment cancelled. Please switch to the desired subscription using:")
        print("   az account set --subscription <subscription-id>")
        sys.exit(0)
    
    print("\n" + "=" * 60)
    print("Resource Configuration")
    print("=" * 60)
    
    # Get user inputs
    resource_name = get_user_input("Enter resource name (will be used as prefix)", "brk445-zava")
    location = get_user_input("Enter Azure region", "eastus2")
    resource_group = f"{resource_name}-rg"
    
    # Get SQL credentials
    print("\n" + "=" * 60)
    print("SQL Server Credentials")
    print("=" * 60)
    admin_user = get_user_input("Enter SQL Server admin username", "sqladmin")
    
    # Get password or generate one
    admin_password = input("Enter SQL Server admin password (min 8 characters) [press Enter to auto-generate]: ").strip()
    
    if not admin_password:
        admin_password = generate_secure_password(16)
        print(f"✅ Generated secure password: {admin_password}")
        print("   (This will be saved to output files)")
    else:
        # Validate password meets requirements
        if len(admin_password) < 8:
            print("❌ Password must be at least 8 characters long.")
            sys.exit(1)
    
    print("\n" + "=" * 60)
    print("Deployment Summary")
    print("=" * 60)
    print(f"Resource Group: {resource_group}")
    print(f"Location: {location}")
    print(f"Resource Prefix: {resource_name}")
    print(f"SQL Admin User: {admin_user}")
    print(f"SQL Admin Password: {admin_password}")
    print("=" * 60)
    
    confirm = input("\nProceed with deployment? (yes/no): ").strip().lower()
    if confirm not in ['yes', 'y']:
        print("\n❌ Deployment cancelled.")
        sys.exit(0)
    
    # Start deployment
    print("\n" + "=" * 60)
    print("Starting Deployment...")
    print("=" * 60)
    
    # Create Resource Group
    if not create_resource_group(resource_group, location):
        print("❌ Failed to create resource group.")
        sys.exit(1)
    
    # Deploy infrastructure using Bicep
    deployment_outputs = deploy_bicep_template(resource_group, resource_name, location, admin_user, admin_password)
    if not deployment_outputs:
        print("❌ Failed to deploy infrastructure.")
        sys.exit(1)
    
    # Get current client IP and add to firewall
    print(f"\n� Configuring SQL Server firewall...")
    print(f"   Detecting your client IP address...")
    try:
        client_ip = run_command("curl -s https://api.ipify.org", check=False)
        if client_ip and client_ip.strip():
            sql_server_name = deployment_outputs.get('sqlServerName')
            print(f"   Your client IP: {client_ip}")
            print(f"   Adding firewall rule...")
            run_command(f"az sql server firewall-rule create --resource-group {resource_group} --server {sql_server_name} --name AllowClientIP --start-ip-address {client_ip} --end-ip-address {client_ip}", check=False)
            print(f"   ✅ Client IP {client_ip} added to firewall.")
        else:
            print(f"   ⚠️  Could not detect client IP. You may need to add firewall rules manually.")
    except Exception as e:
        print(f"   ⚠️  Could not add client IP to firewall: {str(e)}")
    
    # Initialize Database
    print(f"\n📄 Initializing SQL database...")
    database_name = deployment_outputs.get('sqlDatabaseName')
    sql_server_name = deployment_outputs.get('sqlServerName')
    print(f"   Database server: {sql_server_name}")
    print(f"   Database name: {database_name}")
    
    initialize_database(
        sql_server_name,
        database_name,
        admin_user,
        admin_password
    )
    
    # Save deployment information to files
    print(f"\n💾 Saving deployment information...")
    deployment_info = save_deployment_info(
        resource_group,
        location,
        resource_name,
        admin_user,
        admin_password,
        deployment_outputs
    )
    
    # Print summary
    print("\n" + "=" * 60)
    print("🎉 Deployment Complete!")
    print("=" * 60)
    print(f"Resource Group: {resource_group}")
    print(f"Location: {location}")
    print(f"\nApplication Insights:")
    print(f"  Name: {deployment_outputs.get('appInsightsName')}")
    print(f"  Instrumentation Key: {deployment_outputs.get('appInsightsInstrumentationKey')}")
    print(f"\nStorage Account:")
    print(f"  Name: {deployment_outputs.get('storageAccountName')}")
    print(f"\nSQL Server:")
    print(f"  Server: {deployment_outputs.get('sqlServerFqdn')}")
    print(f"  Database: {database_name}")
    print(f"  Admin User: {admin_user}")
    print(f"  Admin Password: {admin_password}")
    print(f"\n📝 Connection String:")
    connection_string = get_connection_string(
        sql_server_name,
        database_name,
        admin_user,
        admin_password
    )
    print(connection_string)
    print("\n⚠️  IMPORTANT: Deployment details saved to files in current directory!")
    print("   - JSON file for programmatic access")
    print("   - TXT file for human-readable reference")
    print("   Keep these files secure and do not commit to source control!")
    print("=" * 60)


if __name__ == "__main__":
    main()
