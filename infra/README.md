# Azure infrastructure

`main.bicep` provisions the portfolio environment: App Service for the API, Azure SQL Database, a Service Bus Standard queue with a dead-letter policy, Application Insights, Container Apps for the worker, and Azure Container Registry.

For a low-cost learning setup, use [`learning.bicep`](learning.bicep). It uses the free Linux F1 App Service plan, Azure SQL Basic, and a Service Bus Basic queue. The worker is run locally against those cloud services when you want to demonstrate processing. It intentionally omits Container Apps, Container Registry and Application Insights.

It deliberately does **not** create your Microsoft Entra application registration or SQL Entra administrator. Those are tenant-level identity actions and must be performed by a tenant administrator/account owner.

## Before the first deployment

1. Sign in with `az login` and select the intended subscription.
2. Create a resource group, then deploy with a unique lowercase prefix:

   ```bash
   az deployment group create \
     --resource-group <resource-group> \
     --template-file infra/main.bicep \
     --parameters namePrefix=<globally-unique-prefix> sqlAdministratorLoginPassword='<strong-password>'
   ```

3. In the SQL server, configure a Microsoft Entra-only administrator. Connect as that administrator and create users for the two managed identities printed as Bicep outputs. Grant the API `db_datareader`, `db_datawriter` and `db_ddladmin` (only if it performs migrations); grant the worker `db_datareader` and `db_datawriter`.
4. Register the API in Microsoft Entra ID, expose an API scope, and add `AzureAd__Instance`, `AzureAd__TenantId`, `AzureAd__ClientId` and `AzureAd__Audience` as App Service settings.

The Bicep template enables the special `0.0.0.0` Azure-services SQL firewall rule for the first learning deployment. For a production system, replace it with private endpoints/VNet integration.
