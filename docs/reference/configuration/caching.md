---
title: Caching
parent: Configuration
grand_parent: Reference
nav_order: 10
---

# Configuration: Caching

To use Memoria's caching features, install and register a caching package.

## In-memory

Install **Memoria.Caching.Memory**:

```csharp
services.AddMemoriaMemoryCache();
```

## Redis

Install **Memoria.Caching.Redis**:

```csharp
services.AddMemoriaRedisCache(options =>
{
    options.ConnectionString = "localhost:6379";
});
```

## Related

- [Memoria Core](memoria.md)
