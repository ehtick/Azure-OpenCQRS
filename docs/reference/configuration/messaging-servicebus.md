---
title: "Messaging: Service Bus"
description: "Register the Azure Service Bus messaging provider, and the in-memory variant for tests."
parent: Configuration
grand_parent: Reference
nav_order: 8
---

# Configuration: Azure Service Bus

To use Service Bus messaging, install and register the **Memoria.Messaging.ServiceBus** package:

```csharp
services.AddMemoriaServiceBus(new ServiceBusOptions
{
    ConnectionString = connectionString
});
```

For local development and tests without an Azure dependency, use **Memoria.Messaging.ServiceBus.InMemory**.

## Related

- [Memoria Core](memoria.md)
- [RabbitMQ](messaging-rabbitmq.md)
