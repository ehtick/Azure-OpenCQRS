---
title: Memoria Core
description: "Register the Memoria mediator core in the service collection, and the options it takes."
parent: Configuration
grand_parent: Reference
nav_order: 1
redirect_from:
  - /Configuration.html
  - /Configuration/
---

# Configuration: Memoria Core

Register Memoria in the service collection (**Memoria** package):

```csharp
services.AddMemoria(typeof(CreateProduct), typeof(GetProduct));
```

All command, query, and notification handlers are registered automatically. Pass one type per assembly that contains handlers — `CreateProduct` is a sample command and `GetProduct` is a sample query, here assumed to live in two different assemblies.

## Optional packages

The core mediator works on its own. Plug in any of the following when you need them:

- **Event Sourcing** — [event-sourcing.md](event-sourcing.md), then pick a store provider:
  - [Entity Framework Core](ef-core.md) (with optional [Identity](ef-core-identity.md) support)
  - [Cosmos DB](cosmos.md)
- **Validation** — [validation.md](validation.md)
- **Messaging** — [Service Bus](messaging-servicebus.md), [RabbitMQ](messaging-rabbitmq.md)
- **Caching** — [caching.md](caching.md)

## Related

- [Quickstart: Mediator](../../getting-started/quickstart-mediator.md) — what this registration gets you
- [Event Sourcing](event-sourcing.md) — the next package up
- [Validation](validation.md)
- [Caching](caching.md)
