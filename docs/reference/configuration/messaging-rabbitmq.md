---
title: "Messaging: RabbitMQ"
parent: Configuration
grand_parent: Reference
nav_order: 9
---

# Configuration: RabbitMQ

To use RabbitMQ messaging, install and register the **Memoria.Messaging.RabbitMq** package:

```csharp
services.AddMemoriaRabbitMq(options =>
{
    options.ConnectionString = connectionString;
});
```

For local development and tests without a RabbitMQ broker, use **Memoria.Messaging.RabbitMq.InMemory**.

## Related

- [Memoria Core](memoria.md)
- [Service Bus](messaging-servicebus.md)
