#!/usr/bin/env python3
"""
Complete Azure Environment Setup for BRK445

Automates the full Azure environment in one run:
  1. Core infrastructure via Bicep (Resource Group, SQL, Storage, App Insights)
  2. Azure AI Services account + Foundry project + model deployments
  3. .NET user secrets for the agent deployer and Aspire AppHost

The AI Services account is created with allowProjectManagement enabled,
and a Foundry project is created as a child resource
(Microsoft.CognitiveServices/accounts/projects — the new non-hub model).

Usage:
  python setup_azure.py                          # Full setup from scratch
  python setup_azure.py --from-deployment <file>  # Use existing infra, add AI + secrets
  python setup_azure.py --skip-infra              # Auto-detect existing deployment info
"""

import subprocess
import json
import sys
import os
import secrets
import string
import tempfile
import argparse
import urllib.request
import urllib.error
from pathlib import Path
from datetime import datetime
from typing import Optional, Dict, Any, List

# ── Paths ────────────────────────────────────────────────────────────────────
SCRIPT_DIR = Path(__file__).parent.resolve()
REPO_ROOT = SCRIPT_DIR.parent
BICEP_FILE = SCRIPT_DIR / "main.bicep"

AGENT_DEPLOYER_CSPROJ = SCRIPT_DIR / "Brk445-Console-DeployAgents.csproj"
APPHOST_CSPROJ = REPO_ROOT / "src" / "ZavaAppHost" / "ZavaAppHost.csproj"

# ── Defaults ─────────────────────────────────────────────────────────────────
DEFAULT_LOCATION = "eastus2"
DEFAULT_RESOURCE_PREFIX = "brk445-zava"
DEFAULT_SQL_ADMIN = "sqladmin"
DEFAULT_CHAT_MODEL = "gpt-5-mini"
DEFAULT_EMBEDDING_MODEL = "text-embedding-3-small"
DEFAULT_PROJECT_NAME = "brk445demo"


# ═══════════════════════════════════════════════════════════════════════════════
#  Helpers
# ═══════════════════════════════════════════════════════════════════════════════

def run(cmd: str, check: bool = True, quiet: bool = True) -> Optional[str]:
    """Run a shell command and return stdout, or None on failure."""
    try:
        r = subprocess.run(cmd, shell=True, check=check, capture_output=True, text=True)
        return r.stdout.strip()
    except subprocess.CalledProcessError as e:
        if not quiet:
            print(f"  ⚠ Command failed: {cmd}")
            if e.stderr:
                for line in e.stderr.strip().splitlines()[:5]:
                    print(f"    {line}")
        return None


def run_dotnet_secret(project: Path, key: str, value: str) -> bool:
    """Set a .NET user secret using subprocess args list (avoids shell escaping)."""
    try:
        subprocess.run(
            ["dotnet", "user-secrets", "set", key, value, "--project", str(project)],
            check=True, capture_output=True, text=True,
        )
        return True
    except subprocess.CalledProcessError as e:
        print(f"  ⚠ Failed to set secret '{key}': {e.stderr.strip()}")
        return False


def prompt(label: str, default: str = "", required: bool = True) -> str:
    """Interactive prompt with optional default."""
    suffix = f" [{default}]" if default else ""
    while True:
        val = input(f"  {label}{suffix}: ").strip()
        if not val and default:
            return default
        if val or not required:
            return val
        print("    This field is required.")


def generate_password(length: int = 16) -> str:
    """Generate a secure random password meeting SQL Server complexity rules."""
    chars = string.ascii_letters + string.digits + "!@#$%&*-_=+"
    while True:
        pw = "".join(secrets.choice(chars) for _ in range(length))
        if (any(c.islower() for c in pw) and any(c.isupper() for c in pw)
                and any(c.isdigit() for c in pw)
                and any(c in "!@#$%&*-_=+" for c in pw)):
            return pw


def banner(title: str):
    print(f"\n{'=' * 60}")
    print(f"  {title}")
    print(f"{'=' * 60}")


def sql_connection_string(fqdn: str, db: str, user: str, password: str) -> str:
    # Quote password to handle special chars ({, }, ;, =) in ADO.NET connection strings
    quoted_pw = f"'{password}'" if any(c in password for c in "{};='\"") else password
    return (
        f"Server=tcp:{fqdn},1433;Initial Catalog={db};"
        f"Persist Security Info=False;User ID={user};Password={quoted_pw};"
        f"MultipleActiveResultSets=False;Encrypt=True;"
        f"TrustServerCertificate=False;Connection Timeout=30;"
    )


# ═══════════════════════════════════════════════════════════════════════════════
#  Phase 0: Prerequisites
# ═══════════════════════════════════════════════════════════════════════════════

def check_prerequisites() -> Dict[str, Any]:
    banner("Checking Prerequisites")

    # Azure CLI
    if run("az --version", check=False) is None:
        print("  ❌ Azure CLI not found. Install: https://aka.ms/installazurecli")
        sys.exit(1)
    print("  ✅ Azure CLI")

    # Logged in
    if run("az account show", check=False) is None:
        print("  ❌ Not logged in. Run: az login")
        sys.exit(1)
    print("  ✅ Authenticated")

    # .NET SDK
    if run("dotnet --version", check=False) is None:
        print("  ❌ .NET SDK not found. Install: https://dotnet.microsoft.com/download")
        sys.exit(1)
    print("  ✅ .NET SDK")

    # Subscription confirmation
    sub = json.loads(run("az account show"))
    print(f"\n  Subscription: {sub['name']}")
    print(f"  ID:           {sub['id']}")
    print(f"  Tenant:       {sub['tenantId']}")

    if prompt("Use this subscription? (yes/no)", "yes").lower() not in ("yes", "y"):
        print("\n  Switch with: az account set --subscription <id>")
        sys.exit(0)

    return sub


# ═══════════════════════════════════════════════════════════════════════════════
#  Phase 1: Infrastructure (Bicep)
# ═══════════════════════════════════════════════════════════════════════════════

def find_deployment_info() -> Optional[Path]:
    """Find the most recent deployment_info JSON file in the infra directory."""
    files = sorted(
        SCRIPT_DIR.glob("deployment_info_*.json"),
        key=lambda f: f.stat().st_mtime,
        reverse=True,
    )
    return files[0] if files else None


def load_deployment_info(path: Path) -> Dict[str, Any]:
    with open(path) as f:
        return json.load(f)


def deploy_infrastructure(resource_prefix: str, location: str,
                          sql_admin: str, sql_password: str) -> Dict[str, Any]:
    """Deploy core infrastructure via the existing Bicep template."""
    resource_group = f"{resource_prefix}-rg"

    print(f"\n  📦 Creating resource group '{resource_group}' in '{location}'...")
    run(f"az group create --name {resource_group} --location {location}")
    print(f"  ✅ Resource group ready")

    print(f"  🚀 Deploying Bicep template (this may take a few minutes)...")

    params = {
        "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
        "contentVersion": "1.0.0.0",
        "parameters": {
            "resourcePrefix": {"value": resource_prefix},
            "location": {"value": location},
            "sqlAdminUsername": {"value": sql_admin},
            "sqlAdminPassword": {"value": sql_password},
        },
    }

    with tempfile.NamedTemporaryFile(mode="w", suffix=".json", delete=False) as f:
        json.dump(params, f)
        params_file = f.name

    try:
        deployment_name = f"{resource_prefix}-deployment"
        result = run(
            f'az deployment group create --resource-group {resource_group} '
            f'--name {deployment_name} --template-file "{BICEP_FILE}" '
            f'--parameters "{params_file}" '
            f'--query "properties.outputs" -o json',
            quiet=False,
        )

        if result is None:
            print("  ❌ Bicep deployment failed.")
            sys.exit(1)

        outputs_raw = json.loads(result)
        outputs = {k: v["value"] for k, v in outputs_raw.items()}
        print("  ✅ Infrastructure deployed")

        # Firewall rule for current client IP
        client_ip = run("curl -s https://api.ipify.org", check=False)
        if client_ip:
            run(
                f"az sql server firewall-rule create --resource-group {resource_group} "
                f"--server {outputs['sqlServerName']} --name AllowClientIP "
                f"--start-ip-address {client_ip} --end-ip-address {client_ip}",
                check=False,
            )
            print(f"  ✅ SQL firewall rule added for {client_ip}")

        conn_str = sql_connection_string(
            outputs["sqlServerFqdn"], outputs["sqlDatabaseName"],
            sql_admin, sql_password,
        )

        # Build and return deployment info in the same format as existing JSON files
        info = {
            "timestamp": datetime.now().isoformat(),
            "resourceGroup": resource_group,
            "location": location,
            "resourcePrefix": resource_prefix,
            "resources": {
                "applicationInsights": {
                    "name": outputs.get("appInsightsName"),
                    "instrumentationKey": outputs.get("appInsightsInstrumentationKey"),
                    "connectionString": outputs.get("appInsightsConnectionString"),
                },
                "storageAccount": {
                    "name": outputs.get("storageAccountName"),
                },
                "sqlServer": {
                    "name": outputs.get("sqlServerName"),
                    "fqdn": outputs.get("sqlServerFqdn"),
                    "adminUsername": sql_admin,
                    "adminPassword": sql_password,
                    "databaseName": outputs.get("sqlDatabaseName"),
                    "connectionString": conn_str,
                },
            },
        }

        # Save infra deployment info (same format as deploy_azure_resources.py)
        ts = datetime.now().strftime("%Y%m%d_%H%M%S")
        info_file = SCRIPT_DIR / f"deployment_info_{resource_prefix}_{ts}.json"
        with open(info_file, "w") as f:
            json.dump(info, f, indent=2)
        print(f"  💾 Infra deployment info saved: {info_file.name}")

        return info
    finally:
        os.unlink(params_file)


# ═══════════════════════════════════════════════════════════════════════════════
#  Phase 2: AI Services (Microsoft Foundry)
# ═══════════════════════════════════════════════════════════════════════════════

def create_ai_services(resource_group: str, name: str, location: str) -> str:
    """Create an Azure AI Services account with project management enabled.
    Returns the AI Foundry API base endpoint (without project path)."""
    print(f"\n  🤖 Creating AI Services account '{name}'...")

    # Check if it already exists
    existing = run(
        f'az cognitiveservices account show --name {name} '
        f'--resource-group {resource_group} '
        f'--query "properties.endpoints.\"AI Foundry API\"" -o tsv',
        check=False,
    )
    if existing:
        print(f"  ✅ AI Services account already exists")
        _ensure_project_management(resource_group, name)
        return existing.rstrip("/")

    result = run(
        f'az cognitiveservices account create --name {name} '
        f'--resource-group {resource_group} --kind AIServices '
        f'--sku S0 --location {location} --custom-domain {name} --yes',
        quiet=False,
    )
    if result is None:
        print("  ❌ Failed to create AI Services account.")
        print("     Check that your subscription has access to AI Services in this region.")
        sys.exit(1)

    _ensure_project_management(resource_group, name)

    # Use the AI Foundry API endpoint (not the generic regional endpoint)
    endpoint = run(
        f'az cognitiveservices account show --name {name} '
        f'--resource-group {resource_group} '
        f'--query "properties.endpoints.\"AI Foundry API\"" -o tsv',
    )
    if not endpoint:
        # Fallback: construct from custom domain
        endpoint = f"https://{name}.services.ai.azure.com"
    print(f"  ✅ AI Services account created")
    return endpoint.rstrip("/")


def _ensure_project_management(resource_group: str, name: str):
    """Enable allowProjectManagement on the AI Services account (required for Foundry projects)."""
    # Check current state
    current = run(
        f'az cognitiveservices account show --name {name} '
        f'--resource-group {resource_group} '
        f'--query "properties.allowProjectManagement" -o tsv',
        check=False,
    )
    if current and current.lower() == "true":
        return

    print(f"  🔧 Enabling project management on AI Services account...")
    token = run("az account get-access-token --resource https://management.azure.com/ --query accessToken -o tsv")
    sub_id = run("az account show --query id -o tsv")
    if not token or not sub_id:
        print("  ⚠️  Could not get access token; enable allowProjectManagement manually in the portal.")
        return

    url = (
        f"https://management.azure.com/subscriptions/{sub_id}/resourceGroups/{resource_group}"
        f"/providers/Microsoft.CognitiveServices/accounts/{name}?api-version=2025-12-01"
    )
    data = json.dumps({"properties": {"allowProjectManagement": True}}).encode()
    req = urllib.request.Request(url, data=data, method="PATCH", headers={
        "Authorization": f"Bearer {token}",
        "Content-Type": "application/json",
    })
    try:
        urllib.request.urlopen(req)
        print(f"  ✅ Project management enabled")
    except Exception as e:
        print(f"  ⚠️  Failed to enable project management: {e}")
        print(f"     You may need to enable allowProjectManagement manually.")


def create_foundry_project(resource_group: str, account_name: str,
                           project_name: str, location: str,
                           base_endpoint: str) -> str:
    """Create a Foundry project as a child of the AI Services account.
    Returns the full project endpoint URL."""
    print(f"\n  📁 Creating Foundry project '{project_name}'...")

    project_endpoint = f"{base_endpoint}/api/projects/{project_name}"

    # Check if it already exists via the project endpoint property
    token = run("az account get-access-token --resource https://management.azure.com/ --query accessToken -o tsv")
    sub_id = run("az account show --query id -o tsv")
    if not token or not sub_id:
        print(f"  ⚠️  Could not get access token. Assuming project endpoint: {project_endpoint}")
        return project_endpoint

    url = (
        f"https://management.azure.com/subscriptions/{sub_id}/resourceGroups/{resource_group}"
        f"/providers/Microsoft.CognitiveServices/accounts/{account_name}"
        f"/projects/{project_name}?api-version=2025-12-01"
    )

    # Check existing
    req = urllib.request.Request(url, method="GET", headers={
        "Authorization": f"Bearer {token}",
    })
    try:
        resp = urllib.request.urlopen(req)
        data = json.loads(resp.read())
        existing_endpoint = (data.get("properties", {})
                             .get("endpoints", {})
                             .get("AI Foundry API", ""))
        if existing_endpoint:
            project_endpoint = existing_endpoint.rstrip("/")
        print(f"  ✅ Foundry project already exists")
        return project_endpoint
    except urllib.error.HTTPError as e:
        if e.code != 404:
            print(f"  ⚠️  Error checking project: HTTP {e.code}")

    # Create the project
    body = json.dumps({
        "location": location,
        "identity": {"type": "SystemAssigned"},
        "properties": {},
    }).encode()
    req = urllib.request.Request(url, data=body, method="PUT", headers={
        "Authorization": f"Bearer {token}",
        "Content-Type": "application/json",
    })
    try:
        resp = urllib.request.urlopen(req)
        data = json.loads(resp.read())
        created_endpoint = (data.get("properties", {})
                            .get("endpoints", {})
                            .get("AI Foundry API", ""))
        if created_endpoint:
            project_endpoint = created_endpoint.rstrip("/")
        print(f"  ✅ Foundry project created")
    except Exception as e:
        print(f"  ⚠️  Failed to create project: {e}")
        print(f"     You may need to create it manually at https://ai.azure.com")
        print(f"     Using assumed endpoint: {project_endpoint}")

    return project_endpoint


def deploy_model(resource_group: str, account_name: str,
                 deployment_name: str, model_name: str,
                 sku_name: str = "GlobalStandard",
                 sku_capacity: int = 10) -> bool:
    """Deploy a model to the AI Services account. Returns True on success."""
    print(f"  📦 Deploying model '{deployment_name}' (model: {model_name}, sku: {sku_name})...")

    # Check if deployment already exists
    existing = run(
        f'az cognitiveservices account deployment show --name {account_name} '
        f'--resource-group {resource_group} '
        f'--deployment-name {deployment_name} --query "name" -o tsv',
        check=False,
    )
    if existing:
        print(f"  ✅ Model deployment '{deployment_name}' already exists")
        return True

    result = run(
        f'az cognitiveservices account deployment create --name {account_name} '
        f'--resource-group {resource_group} '
        f'--deployment-name {deployment_name} '
        f'--model-name {model_name} --model-format OpenAI '
        f'--sku-name {sku_name} --sku-capacity {sku_capacity}',
        quiet=False, check=False,
    )

    if result is None:
        print(f"  ⚠️  Model deployment '{deployment_name}' failed.")
        print(f"     The model '{model_name}' may not be available in your region or quota.")
        print(f"     You can deploy it manually at https://ai.azure.com")
        return False

    print(f"  ✅ Model '{deployment_name}' deployed")
    return True


def get_ai_key(resource_group: str, account_name: str) -> str:
    """Retrieve the primary API key for the AI Services account."""
    key = run(
        f'az cognitiveservices account keys list --name {account_name} '
        f'--resource-group {resource_group} --query "key1" -o tsv',
    )
    return key or ""


# ═══════════════════════════════════════════════════════════════════════════════
#  Phase 3: .NET User Secrets
# ═══════════════════════════════════════════════════════════════════════════════

def configure_secrets(project_endpoint: str, ai_connection_string: str,
                      chat_deployment: str, tenant_id: str,
                      sql_connection_string: str,
                      appinsights_connection_string: str):
    """Set .NET user secrets for both the agent deployer and Aspire AppHost."""
    banner("Configuring .NET User Secrets")

    # ── Agent Deployer (infra/Brk445-Console-DeployAgents.csproj) ──
    if AGENT_DEPLOYER_CSPROJ.exists():
        print(f"\n  📋 Agent Deployer ({AGENT_DEPLOYER_CSPROJ.name})")
        run_dotnet_secret(AGENT_DEPLOYER_CSPROJ, "ProjectEndpoint", project_endpoint)
        run_dotnet_secret(AGENT_DEPLOYER_CSPROJ, "ModelDeploymentName", chat_deployment)
        if tenant_id:
            run_dotnet_secret(AGENT_DEPLOYER_CSPROJ, "TenantId", tenant_id)
        if sql_connection_string:
            run_dotnet_secret(AGENT_DEPLOYER_CSPROJ, "SqlServerConnectionString", sql_connection_string)
        print("  ✅ Agent deployer secrets set")
    else:
        print(f"  ⚠️  Agent deployer project not found at {AGENT_DEPLOYER_CSPROJ}")

    # ── Aspire AppHost (src/ZavaAppHost/ZavaAppHost.csproj) ──
    if APPHOST_CSPROJ.exists():
        print(f"\n  📋 Aspire AppHost ({APPHOST_CSPROJ.name})")
        run_dotnet_secret(APPHOST_CSPROJ, "ConnectionStrings:microsoftfoundryproject", project_endpoint)
        if tenant_id:
            run_dotnet_secret(APPHOST_CSPROJ, "ConnectionStrings:tenantId", tenant_id)
        if appinsights_connection_string:
            run_dotnet_secret(APPHOST_CSPROJ, "ConnectionStrings:appinsights", appinsights_connection_string)
        print("  ✅ AppHost secrets set")
    else:
        print(f"  ⚠️  AppHost project not found at {APPHOST_CSPROJ}")


# ═══════════════════════════════════════════════════════════════════════════════
#  Output
# ═══════════════════════════════════════════════════════════════════════════════

def save_full_deployment_info(infra_info: Dict[str, Any],
                              ai_info: Dict[str, Any]) -> Path:
    """Save comprehensive deployment information combining infra + AI."""
    prefix = infra_info.get("resourcePrefix", "brk445")
    ts = datetime.now().strftime("%Y%m%d_%H%M%S")
    filename = SCRIPT_DIR / f"full_deployment_info_{prefix}_{ts}.json"

    full = {
        **infra_info,
        "aiServices": ai_info,
        "secretsConfiguredFor": [
            str(AGENT_DEPLOYER_CSPROJ) if AGENT_DEPLOYER_CSPROJ.exists() else None,
            str(APPHOST_CSPROJ) if APPHOST_CSPROJ.exists() else None,
        ],
    }

    with open(filename, "w") as f:
        json.dump(full, f, indent=2)

    print(f"\n  💾 Full deployment info saved: {filename.name}")
    return filename


# ═══════════════════════════════════════════════════════════════════════════════
#  Main
# ═══════════════════════════════════════════════════════════════════════════════

def main():
    parser = argparse.ArgumentParser(
        description="Complete Azure Environment Setup for BRK445",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python setup_azure.py                                    # Full setup
  python setup_azure.py --from-deployment deployment_info_brk445_20260414.json
  python setup_azure.py --skip-infra                       # AI + secrets only
  python setup_azure.py --chat-model gpt-4o-mini           # Override model names
        """,
    )
    parser.add_argument(
        "--from-deployment", type=str, metavar="FILE",
        help="Path to an existing deployment_info JSON file (skips infrastructure)",
    )
    parser.add_argument(
        "--skip-infra", action="store_true",
        help="Skip infrastructure deployment (auto-detect existing deployment info)",
    )
    parser.add_argument("--chat-model", default=DEFAULT_CHAT_MODEL, help=f"Chat model name (default: {DEFAULT_CHAT_MODEL})")
    parser.add_argument("--embedding-model", default=DEFAULT_EMBEDDING_MODEL, help=f"Embedding model name (default: {DEFAULT_EMBEDDING_MODEL})")
    parser.add_argument("--chat-sku", default="GlobalStandard", help="Chat model SKU (default: GlobalStandard)")
    parser.add_argument("--embedding-sku", default="Standard", help="Embedding model SKU (default: Standard)")
    parser.add_argument("--sku-capacity", type=int, default=10, help="Model SKU capacity in K TPM (default: 10)")
    parser.add_argument("--project-name", default=DEFAULT_PROJECT_NAME, help=f"Foundry project name (default: {DEFAULT_PROJECT_NAME})")
    args = parser.parse_args()

    banner("BRK445 — Complete Azure Environment Setup")
    print("  This script provisions all Azure resources and configures")
    print("  .NET user secrets so you're ready to deploy agents and run Aspire.")

    # ── Phase 0: Prerequisites ──
    sub = check_prerequisites()
    tenant_id = sub.get("tenantId", "")

    # ── Phase 1: Infrastructure ──
    infra_info = None

    if args.from_deployment:
        path = Path(args.from_deployment)
        if not path.is_absolute():
            path = SCRIPT_DIR / path
        if not path.exists():
            print(f"  ❌ File not found: {path}")
            sys.exit(1)
        infra_info = load_deployment_info(path)
        print(f"\n  📂 Using existing deployment: {path.name}")
        print(f"     Resource Group: {infra_info.get('resourceGroup')}")
        print(f"     Location:       {infra_info.get('location')}")

    elif args.skip_infra:
        existing = find_deployment_info()
        if existing:
            infra_info = load_deployment_info(existing)
            print(f"\n  📂 Found existing deployment: {existing.name}")
            print(f"     Resource Group: {infra_info.get('resourceGroup')}")
            print(f"     Location:       {infra_info.get('location')}")
        else:
            print("  ❌ No existing deployment_info file found. Remove --skip-infra to deploy.")
            sys.exit(1)

    else:
        # Check for existing deployment before prompting
        existing = find_deployment_info()
        if existing:
            info = load_deployment_info(existing)
            print(f"\n  📂 Found existing deployment: {existing.name}")
            print(f"     Resource Group: {info.get('resourceGroup')}")
            print(f"     Location:       {info.get('location')}")
            choice = prompt("Use existing infrastructure? (yes/no)", "yes")
            if choice.lower() in ("yes", "y"):
                infra_info = info

    if infra_info is None:
        banner("Infrastructure Configuration")
        resource_prefix = prompt("Resource name prefix", DEFAULT_RESOURCE_PREFIX)
        location = prompt("Azure region", DEFAULT_LOCATION)
        sql_admin = prompt("SQL admin username", DEFAULT_SQL_ADMIN)
        sql_password = input(f"  SQL admin password [Enter to auto-generate]: ").strip()
        if not sql_password:
            sql_password = generate_password()
            print(f"  ✅ Generated password: {sql_password}")

        # Confirmation
        print(f"\n  Resource Group:  {resource_prefix}-rg")
        print(f"  Location:        {location}")
        print(f"  SQL Admin:       {sql_admin}")
        confirm = prompt("Proceed with infrastructure deployment? (yes/no)", "yes")
        if confirm.lower() not in ("yes", "y"):
            print("  Cancelled.")
            sys.exit(0)

        banner("Deploying Infrastructure")
        infra_info = deploy_infrastructure(resource_prefix, location, sql_admin, sql_password)

    # Extract values from infra_info
    resource_group = infra_info["resourceGroup"]
    location = infra_info.get("location", DEFAULT_LOCATION)
    resource_prefix = infra_info.get("resourcePrefix", resource_group.replace("-rg", ""))
    sql_conn = infra_info.get("resources", {}).get("sqlServer", {}).get("connectionString", "")
    appinsights_conn = infra_info.get("resources", {}).get("applicationInsights", {}).get("connectionString", "")

    # ── Phase 2: AI Services + Foundry Project ──
    banner("AI Services (Microsoft Foundry)")

    ai_services_name = f"{resource_prefix}-aiservices"
    chat_model = args.chat_model
    embedding_model = args.embedding_model

    base_endpoint = create_ai_services(resource_group, ai_services_name, location)

    project_endpoint = create_foundry_project(
        resource_group, ai_services_name,
        args.project_name, location, base_endpoint,
    )

    print()
    chat_ok = deploy_model(
        resource_group, ai_services_name,
        deployment_name=chat_model, model_name=chat_model,
        sku_name=args.chat_sku, sku_capacity=args.sku_capacity,
    )
    embed_ok = deploy_model(
        resource_group, ai_services_name,
        deployment_name=embedding_model, model_name=embedding_model,
        sku_name=args.embedding_sku, sku_capacity=args.sku_capacity,
    )

    api_key = get_ai_key(resource_group, ai_services_name)
    ai_connection_string = f"Endpoint={base_endpoint};Key={api_key}"

    print(f"\n  Endpoint:  {project_endpoint}")
    print(f"  API Key:   {api_key[:8]}{'*' * (len(api_key) - 8)}" if len(api_key) > 8 else "")

    # ── Phase 3: .NET User Secrets ──
    configure_secrets(
        project_endpoint=project_endpoint,
        ai_connection_string=ai_connection_string,
        chat_deployment=chat_model,
        tenant_id=tenant_id,
        sql_connection_string=sql_conn,
        appinsights_connection_string=appinsights_conn,
    )

    # ── Save comprehensive output ──
    ai_info = {
        "name": ai_services_name,
        "endpoint": base_endpoint,
        "projectEndpoint": project_endpoint,
        "projectName": args.project_name,
        "apiKey": api_key,
        "connectionString": ai_connection_string,
        "chatDeployment": {"name": chat_model, "deployed": chat_ok},
        "embeddingDeployment": {"name": embedding_model, "deployed": embed_ok},
    }
    save_full_deployment_info(infra_info, ai_info)

    # ── Summary ──
    banner("Setup Complete! 🎉")
    print(f"  Resource Group:   {resource_group}")
    print(f"  AI Services:      {ai_services_name}")
    print(f"  Foundry Project:  {args.project_name}")
    print(f"  Endpoint:         {project_endpoint}")
    print(f"  Chat Model:       {chat_model} {'✅' if chat_ok else '⚠️  (deploy manually)'}")
    print(f"  Embedding Model:  {embedding_model} {'✅' if embed_ok else '⚠️  (deploy manually)'}")
    if sql_conn:
        print(f"  SQL Server:       ✅ configured")
    if appinsights_conn:
        print(f"  App Insights:     ✅ configured")
    print(f"  .NET Secrets:     ✅ configured")

    print(f"\n  Next steps:")
    print(f"    1. Deploy agents:      cd infra && dotnet run")
    print(f"    2. Run the app:        cd src && aspire run")
    print(f"{'=' * 60}\n")


if __name__ == "__main__":
    main()
