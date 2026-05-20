@description('Short project prefix used in Azure resource names.')
param prefix string

@description('Azure location for all resources.')
param location string = resourceGroup().location

@allowed([
  'Free_F1'
  'Standard_S1'
])
@description('Azure SignalR SKU. Free_F1 is the cheapest starting point.')
param signalrSkuName string = 'Free_F1'

var storageAccountName = toLower(replace('${prefix}st${uniqueString(resourceGroup().id)}', '-', ''))
var signalRName = '${prefix}-signalr'
var functionPlanName = '${prefix}-plan'
var functionAppName = '${prefix}-func'
var appInsightsName = '${prefix}-appi'
var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: true
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
  properties: {
    cors: {
      corsRules: [
        {
          allowedOrigins: [
            '*'
          ]
          allowedMethods: [
            'GET'
            'HEAD'
            'OPTIONS'
          ]
          allowedHeaders: [
            '*'
          ]
          exposedHeaders: [
            '*'
          ]
          maxAgeInSeconds: 3600
        }
      ]
    }
    deleteRetentionPolicy: {
      enabled: true
      days: 7
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }
}

resource signalR 'Microsoft.SignalRService/signalR@2024-03-01-preview' = {
  name: signalRName
  location: location
  sku: {
    name: signalrSkuName
    capacity: 1
  }
  properties: {
    features: [
      {
        flag: 'ServiceMode'
        value: 'Serverless'
      }
    ]
    cors: {
      allowedOrigins: [
        '*'
      ]
    }
    tls: {
      clientCertEnabled: false
    }
  }
}

resource functionPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: functionPlanName
  location: location
  kind: 'functionapp'
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {}
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: functionPlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'AzureWebJobsStorage'
          value: storageConnectionString
        }
        {
          name: 'OverlayStorageConnectionString'
          value: storageConnectionString
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'Bot:OverlayBaseUrl'
          value: storageAccount.properties.primaryEndpoints.web
        }
        {
          name: 'Bot:OverlayApiBaseUrl'
          value: 'https://${functionAppName}.azurewebsites.net'
        }
        {
          name: 'Bot:OverlayAssetsBasePath'
          value: '/overlay-assets'
        }
        {
          name: 'Bot:OverlayStaticContainer'
          value: '$web'
        }
        {
          name: 'Bot:OverlayControlContainer'
          value: 'control'
        }
        {
          name: 'Bot:SignalRConnectionString'
          value: signalR.listKeys().primaryConnectionString
        }
        {
          name: 'Bot:SignalRHubName'
          value: 'overlay'
        }
        {
          name: 'Bot:SignalRTokenLifetimeMinutes'
          value: '10'
        }
      ]
    }
  }
}

output functionAppName string = functionApp.name
output functionAppUrl string = 'https://${functionApp.name}.azurewebsites.net'
output overlayBaseUrl string = storageAccount.properties.primaryEndpoints.web
output storageAccountName string = storageAccount.name
output signalRName string = signalR.name