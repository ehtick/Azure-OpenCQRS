---
title: Validation
parent: Configuration
grand_parent: Reference
nav_order: 7
---

# Configuration: Validation

To use Memoria's command validation features, install and register a validation package.

## FluentValidation

Install **Memoria.Validation.FluentValidation**:

```csharp
services.AddMemoriaFluentValidation(typeof(CreateProduct));
```

All validators are registered automatically. Pass one type per assembly that contains validators.

## Related

- [Memoria Core](memoria.md)
