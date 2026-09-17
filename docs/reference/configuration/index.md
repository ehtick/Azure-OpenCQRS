---
title: Configuration
description: "One page per package: the Add call that registers it, the settings it reads, and their defaults."
parent: Reference
nav_order: 4
has_children: true
---

# Configuration

One page per package: the `Add…` call that registers it, the settings it reads, and their defaults.

Every Memoria package follows the same shape — an `AddMemoria…` extension on `IServiceCollection`
that takes the assemblies to scan, and optionally an options delegate. Start with
[Memoria Core](memoria.md), which every other package assumes is registered.
