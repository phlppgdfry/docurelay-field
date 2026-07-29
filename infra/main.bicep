targetScope = 'resourceGroup'

@description('Short globally unique prefix, for example drphilippe001.')
param namePrefix string

param location string = resourceGroup().location

@secure()
@description('Only used to create the logical SQL server. The running apps authenticate with managed identities.')
param sqlAdministratorLoginPassword string

param sqlAdministratorLogin string = 'docurelayadmin'

@description('A placeholder image is replaced by the deployment workflow with the built DocuRelay worker image.')
param workerImage string = 'mcr.microsoft.com/k8se/quickstart:latest'

var tags = {
  application: 'DocuRelay Field'
  managedBy: 'Bicep'
  purpose: 'portfolio-learning'
}
var appPlanName = '${namePrefix}-plan'
var apiName = '${namePrefix}-api'
var sqlServerName = '${namePrefix}-sql'
var databaseName = 'docurelay'
var serviceBusName = '${namePrefix}-bus'
var registryName = replace('${namePrefix}acr', '-', '')
var containerEnvironmentName = '${namePrefix}-env'
var workerName = '${namePrefix}-worker'

resource logWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${namePrefix}-logs'
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${namePrefix}-appi'
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logWorkspace.id
  }
}

resource appPlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: appPlanName
  location: location
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  kind: 'linux'
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
      alwaysOn: true
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsights.properties.ConnectionString
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

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  tags: tags
  properties: {
    administratorLogin: sqlAdministratorLogin
    administratorLoginPassword: sqlAdministratorLoginPassword
    version: '12.0'
    publicNetworkAccess: 'Enabled'
    minimalTlsVersion: '1.2'
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

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  tags: tags
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
  }
}

resource serviceBus 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: serviceBusName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
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

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: registryName
  location: location
  sku: { name: 'Basic' }
  tags: tags
  properties: {
    adminUserEnabled: false
  }
}

resource containerEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: containerEnvironmentName
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logWorkspace.properties.customerId
        sharedKey: logWorkspace.listKeys().primarySharedKey
      }
    }
  }
}

resource worker 'Microsoft.App/containerApps@2024-03-01' = {
  name: workerName
  location: location
  identity: { type: 'SystemAssigned' }
  tags: tags
  properties: {
    managedEnvironmentId: containerEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      registries: [
        {
          server: registry.properties.loginServer
          identity: 'system'
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'document-worker'
          image: workerImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
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
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 3
      }
    }
  }
}

var serviceBusDataSenderRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39')
var serviceBusDataReceiverRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'c697a2f9-7f24-4b23-bf5b-5c5d408aafbc')
var acrPullRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')

resource apiServiceBusSender 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: serviceBus
  name: guid(serviceBus.id, api.id, serviceBusDataSenderRoleId)
  properties: {
    roleDefinitionId: serviceBusDataSenderRoleId
    principalId: api.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource workerServiceBusReceiver 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: serviceBus
  name: guid(serviceBus.id, worker.id, serviceBusDataReceiverRoleId)
  properties: {
    roleDefinitionId: serviceBusDataReceiverRoleId
    principalId: worker.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource workerAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: registry
  name: guid(registry.id, worker.id, acrPullRoleId)
  properties: {
    roleDefinitionId: acrPullRoleId
    principalId: worker.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output apiUrl string = 'https://${api.properties.defaultHostName}'
output apiPrincipalId string = api.identity.principalId
output workerPrincipalId string = worker.identity.principalId
output sqlServerFqdn string = '${sqlServerName}.database.windows.net'
output registryLoginServer string = registry.properties.loginServer
