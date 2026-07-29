targetScope = 'resourceGroup'

@description('Globally unique lowercase prefix, for example docurelayf3bcf4e.')
param namePrefix string

param location string = resourceGroup().location

@description('Microsoft Entra object ID of the Azure SQL administrator.')
param sqlAdministratorObjectId string

@description('Microsoft Entra UPN of the Azure SQL administrator.')
param sqlAdministratorLogin string

var appPlanName = '${namePrefix}-f1'
var apiName = '${namePrefix}-api'
var sqlServerName = '${namePrefix}-sql'
var databaseName = 'docurelay'
var serviceBusName = '${namePrefix}-bus'
var tags = {
  application: 'DocuRelay Field'
  environment: 'learning'
  costProfile: 'low'
}

// F1 is free but intentionally has no Always On. A first request after idling can therefore be slower.
resource appPlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: appPlanName
  location: location
  kind: 'linux'
  sku: {
    name: 'F1'
    tier: 'Free'
  }
  tags: tags
  properties: {
    reserved: true
  }
}

resource api 'Microsoft.Web/sites@2024-04-01' = {
  name: apiName
  location: location
  kind: 'app,linux'
  identity: { type: 'SystemAssigned' }
  tags: tags
  properties: {
    serverFarmId: appPlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'ConnectionStrings__AzureSql'
          value: 'Server=tcp:${sqlServerName}.database.windows.net,1433;Initial Catalog=${databaseName};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Default;'
        }
        {
          name: 'ServiceBus__FullyQualifiedNamespace'
          value: '${serviceBusName}.servicebus.windows.net'
        }
        {
          name: 'ServiceBus__QueueName'
          value: 'document-processing'
        }
      ]
    }
  }
}

// Passwordless SQL server: only the supplied Microsoft Entra administrator can initially connect.
resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  tags: tags
  properties: {
    publicNetworkAccess: 'Enabled'
    minimalTlsVersion: '1.2'
    administrators: {
      administratorType: 'ActiveDirectory'
      login: sqlAdministratorLogin
      sid: sqlAdministratorObjectId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
    }
  }
}

resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Basic is paid but very small. It is retained so the project genuinely uses Azure SQL Database.
resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
}

// Basic supports the one queue and DLQ needed for this learning project. Standard is not required here.
resource serviceBus 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: serviceBusName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  tags: tags
  properties: {
    publicNetworkAccess: 'Enabled'
    minimumTlsVersion: '1.2'
  }
}

resource processingQueue 'Microsoft.ServiceBus/namespaces/queues@2024-01-01' = {
  parent: serviceBus
  name: 'document-processing'
  properties: {
    lockDuration: 'PT1M'
    maxDeliveryCount: 5
    deadLetteringOnMessageExpiration: true
    defaultMessageTimeToLive: 'P14D'
  }
}

var serviceBusDataSenderRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39')

resource apiServiceBusSender 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: serviceBus
  name: guid(serviceBus.id, api.id, serviceBusDataSenderRoleId)
  properties: {
    roleDefinitionId: serviceBusDataSenderRoleId
    principalId: api.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output apiUrl string = 'https://${api.properties.defaultHostName}'
output apiPrincipalId string = api.identity.principalId
output sqlServerFqdn string = '${sqlServerName}.database.windows.net'
output serviceBusNamespace string = '${serviceBusName}.servicebus.windows.net'
