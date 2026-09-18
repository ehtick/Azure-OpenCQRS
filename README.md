# Memoria&trade;

[![Build](https://github.com/lucabriguglia/Memoria/actions/workflows/build.yml/badge.svg)](https://github.com/lucabriguglia/Memoria/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Memoria?label=nuget%20stable)](https://www.nuget.org/packages/Memoria)
[![NuGet pre-release](https://img.shields.io/nuget/vpre/Memoria?label=nuget%20pre-release)](https://www.nuget.org/packages/Memoria/absoluteLatest)
[![Downloads](https://img.shields.io/nuget/dt/Memoria?label=downloads)](https://www.nuget.org/packages/Memoria)
[![Licence](https://img.shields.io/badge/licence-RPL--1.5%20OR%20Commercial-blue)](https://lucabriguglia.github.io/Memoria/license.html)

**Event sourcing for .NET with two consistency models in one framework.** Store state as classic
event streams, or as dynamic consistency boundaries where the boundary is a tag query chosen per
decision. Start with the mediator alone and add an event store when you need one — nothing forces
you to take both.

From Latin _memoria_ (memory).

📘 [Documentation](https://lucabriguglia.github.io/Memoria/) ·
🚀 [Getting started](https://lucabriguglia.github.io/Memoria/getting-started/) ·
💡 [Concepts](https://lucabriguglia.github.io/Memoria/concepts/) ·
🧭 [Guides](https://lucabriguglia.github.io/Memoria/guides/) ·
📗 [Reference](https://lucabriguglia.github.io/Memoria/reference/)

📚 [Examples](https://lucabriguglia.github.io/Memoria/examples.html) ·
🔎 [Memoria Web](https://lucabriguglia.github.io/Memoria/tools/memoria-web.html) ·
⬆️ [Upgrading](https://lucabriguglia.github.io/Memoria/upgrading.html) ·
📣 [Release notes](https://lucabriguglia.github.io/Memoria/release-notes.html)

## 📄 Licence at a glance

Memoria 2.x is dual-licensed. You choose which licence you use it under.

| Your situation | Licence | Cost |
|----------------|---------|------|
| You release the source of what you build under the same licence | [Reciprocal Public License 1.5](https://opensource.org/license/rpl-1-5) (OSI-approved) | Free |
| Closed source, under $5,000,000 USD annual revenue, or a non-profit under $5,000,000 USD annual budget | [Memoria Commercial Licence](https://lucabriguglia.github.io/Memoria/license.html) — **Community** edition | Free, and always will be |
| Closed source, above that threshold | Memoria Commercial Licence — Standard, Professional or Enterprise | [From $299 USD/year](https://lucabriguglia.github.io/Memoria/pricing.html) |

Versions 1.x remain under the [Apache License 2.0](https://github.com/lucabriguglia/Memoria/blob/1.9.1/LICENSE).
Exclusions and full terms are in [the licence section below](#-licence) and on the
[licence page](https://lucabriguglia.github.io/Memoria/license.html); the editions, what each covers
and how to buy one are on the [pricing page](https://lucabriguglia.github.io/Memoria/pricing.html).

**Already on 1.x?**
[Upgrade to 2.0.0](https://lucabriguglia.github.io/Memoria/guides/upgrade-2.0.0.html) walks through
the licence decision and the two behaviours that move with it. Nothing in the store changes, so
there is no data migration.

## 📥 Install

```bash
dotnet add package Memoria
```

Only `Memoria` is required. Add an event-sourcing package and a store when you need one:

```bash
dotnet add package Memoria.EventSourcing
dotnet add package Memoria.EventSourcing.Store.EntityFrameworkCore
```

2.0.0 is currently in beta, so add `--prerelease` to install it. The
[install guide](https://lucabriguglia.github.io/Memoria/getting-started/install.html) explains when
to reach for each of the [packages listed below](#-packages).

## 🔄 Quickstart

Register what you need at startup:

```csharp
services.AddMemoria(typeof(Program));                    // Mediator: commands, queries, notifications
services.AddMemoriaEventSourcing(typeof(Program));       // Aggregates, streams, projections
services.AddMemoriaEntityFrameworkCore<MyDbContext>();   // Store
```

### As a mediator

```csharp
public record CreateProduct(string Name) : ICommand;

public class CreateProductHandler : ICommandHandler<CreateProduct>
{
    public Task<Result> Handle(CreateProduct command, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Ok());
}

await dispatcher.Send(new CreateProduct("Espresso"));
```

See the [Mediator quickstart](https://lucabriguglia.github.io/Memoria/getting-started/quickstart-mediator.html)
for queries, notifications, validation, custom handlers, command sequences and `SendAndPublish`.

### As an event store

```csharp
[EventType("OrderPlaced")]
public record OrderPlacedEvent(Guid OrderId, decimal Amount) : IEvent;

var streamId = new CustomerStreamId(customerId);
var aggregateId = new OrderId(orderId);
var order = new Order(orderId, amount: 25.45m);

await domainService.SaveAggregate(streamId, aggregateId, order, expectedEventSequence: 0);
```

See the [Event Sourcing quickstart](https://lucabriguglia.github.io/Memoria/getting-started/quickstart-event-sourcing.html)
for the full aggregate definition, and
[Streams or DCB?](https://lucabriguglia.github.io/Memoria/guides/choose-streams-or-dcb.html) for
choosing a consistency model.

Past that, the [guides](https://lucabriguglia.github.io/Memoria/guides/) cover one task each, and
the [reference](https://lucabriguglia.github.io/Memoria/reference/) has the
[`IDomainService` API](https://lucabriguglia.github.io/Memoria/reference/domain-service.html) and a
[configuration page per package](https://lucabriguglia.github.io/Memoria/reference/configuration/).

## ⚡ What you get

| Area | Capabilities |
|------|--------------|
| **Mediator** | Commands, queries and notifications through one `IDispatcher`; command sequences; custom handlers in place of the resolved one; automatic notification publication after a command succeeds; [`Result` instead of exceptions](https://lucabriguglia.github.io/Memoria/concepts/result-pattern.html) |
| **Consistency** | [Event streams](https://lucabriguglia.github.io/Memoria/concepts/aggregates-and-streams.html) with optimistic concurrency on an expected event sequence, or [dynamic consistency boundaries](https://lucabriguglia.github.io/Memoria/concepts/dynamic-consistency-boundaries.html) where the boundary is a tag query chosen per decision; [multiple aggregates per stream](https://lucabriguglia.github.io/Memoria/guides/multiple-aggregates-per-stream.html) |
| **Reads** | [Four read modes](https://lucabriguglia.github.io/Memoria/concepts/read-modes.html); aggregate snapshots stored alongside events for fast, strongly consistent reads; [projections](https://lucabriguglia.github.io/Memoria/concepts/projections.html) persisted and retrieved as snapshots; [in-memory reconstruction](https://lucabriguglia.github.io/Memoria/guides/replay-events-in-memory.html) up to a given sequence or date |
| **Event queries** | Filter applied events by event type, or by event property declared as key/value pairs on the aggregate id; query stream events from or up to a sequence, date or date range; retrieve every event applied to an aggregate |
| **Providers** | Stores: EF Core (plus [ASP.NET Identity](https://lucabriguglia.github.io/Memoria/guides/integrate-aspnet-identity.html) and [PostgreSQL `jsonb`](https://lucabriguglia.github.io/Memoria/guides/use-postgres-jsonb.html) companions), Cosmos DB. Messaging: Azure Service Bus, RabbitMQ. Caching: in-memory, Redis. Validation: FluentValidation |
| **Testing** | [In-memory variants](https://lucabriguglia.github.io/Memoria/guides/test-without-external-deps.html) of Cosmos DB, Service Bus and RabbitMQ, so a test suite needs no external dependency |
| **Tooling** | [Memoria Web](#-memoria-web) — read any Memoria store through your own domain assemblies |

## 🔎 Memoria Web

A browser tool for reading a Memoria store. Point it at a database, upload a zip of **your own**
domain assemblies — with a `memoria.json` at its root naming the services in it — and it shows you
the events that were appended, the aggregates and projections snapshotted from them, and the types
both were written through, for both consistency models side by side.

```bash
dotnet run --project src/Memoria.Web
```

![A CustomerAccount aggregate folded from its events, on the State tab](https://raw.githubusercontent.com/lucabriguglia/Memoria/main/docs/images/memoria-web/aggregate-state.png)

Every section has a **Types** page listing what the uploaded assemblies declare, and a **Data** page
listing what the store actually holds — filtered, sorted and paged, with all of it in the query
string so a view can be bookmarked and shared. Open a row and the aggregate is folded from its
events, so you can see the state a snapshot stands at and how far behind its stream it is.

It ships in the repository rather than on NuGet, so you build and run it yourself. It creates
nothing and deletes nothing: the only write it offers is refreshing a snapshot that has fallen
behind its stream or its boundary.

There is also a hosted instance at [demo.getmemoria.io](https://demo.getmemoria.io), if you would
rather try it than build it. It is behind its sign-in, so access is by invitation: message me on
[LinkedIn](https://www.linkedin.com/in/lucabriguglia) and I will send you one.

Operators sign in through an OpenID Connect provider, and every page and update is behind one of
three roles — Reader, Updater, Administrator — granted for every service by configuration or for a
single service by its manifest. Setting `Authentication:Disabled` runs the tool open instead, with
nobody signed in and nothing withheld.

> **Uploading an assembly runs its code in the tool's process.** Upload only assemblies you trust,
> and keep an open instance on localhost or behind a proxy that authenticates every request.

To try it without a domain of your own, `src/Memoria.Web.Samples.Streamed` and
`src/Memoria.Web.Samples.Dcb` carry a sample ecommerce domain modelled once in each consistency
model, and `src/Memoria.Web.Samples` fills a store with data written through it.

🔎 [What it is, and what each page shows](https://lucabriguglia.github.io/Memoria/tools/memoria-web.html) ·
⚙️ [Configuration](https://lucabriguglia.github.io/Memoria/tools/memoria-web-configuration.html) ·
🚢 [Deployment](https://lucabriguglia.github.io/Memoria/tools/memoria-web-deployment.html) ·
🌱 [Sample data](https://lucabriguglia.github.io/Memoria/tools/memoria-web-samples.html)

## 📦 Packages

Badges show the latest stable release. 2.0.0-beta is published as a pre-release on every package.

| Package | Version | What it adds |
|---------|---------|--------------|
| [Memoria](https://www.nuget.org/packages/Memoria) | [![NuGet](https://img.shields.io/nuget/v/Memoria)](https://www.nuget.org/packages/Memoria) | **Required.** Mediator core: commands, queries, notifications, dispatcher |
| [Memoria.EventSourcing](https://www.nuget.org/packages/Memoria.EventSourcing) | [![NuGet](https://img.shields.io/nuget/v/Memoria.EventSourcing)](https://www.nuget.org/packages/Memoria.EventSourcing) | Aggregates, streams, projections and `IDomainService` |
| [Memoria.EventSourcing.Dcb](https://www.nuget.org/packages/Memoria.EventSourcing.Dcb) | [![NuGet](https://img.shields.io/nuget/v/Memoria.EventSourcing.Dcb)](https://www.nuget.org/packages/Memoria.EventSourcing.Dcb) | Dynamic consistency boundaries |
| [Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore](https://www.nuget.org/packages/Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore) | [![NuGet](https://img.shields.io/nuget/v/Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore)](https://www.nuget.org/packages/Memoria.EventSourcing.Dcb.Store.EntityFrameworkCore) | EF Core store for the DCB model |
| [Memoria.EventSourcing.Store.EntityFrameworkCore](https://www.nuget.org/packages/Memoria.EventSourcing.Store.EntityFrameworkCore) | [![NuGet](https://img.shields.io/nuget/v/Memoria.EventSourcing.Store.EntityFrameworkCore)](https://www.nuget.org/packages/Memoria.EventSourcing.Store.EntityFrameworkCore) | EF Core store: SQL Server, SQLite, PostgreSQL, MySQL, in-memory |
| [Memoria.EventSourcing.Store.EntityFrameworkCore.Identity](https://www.nuget.org/packages/Memoria.EventSourcing.Store.EntityFrameworkCore.Identity) | [![NuGet](https://img.shields.io/nuget/v/Memoria.EventSourcing.Store.EntityFrameworkCore.Identity)](https://www.nuget.org/packages/Memoria.EventSourcing.Store.EntityFrameworkCore.Identity) | The above, plus ASP.NET Core Identity in the same `DbContext` |
| [Memoria.EventSourcing.Store.EntityFrameworkCore.Npgsql](https://www.nuget.org/packages/Memoria.EventSourcing.Store.EntityFrameworkCore.Npgsql) | [![NuGet](https://img.shields.io/nuget/v/Memoria.EventSourcing.Store.EntityFrameworkCore.Npgsql)](https://www.nuget.org/packages/Memoria.EventSourcing.Store.EntityFrameworkCore.Npgsql) | PostgreSQL `jsonb`-aware event-property filtering |
| [Memoria.EventSourcing.Store.Cosmos](https://www.nuget.org/packages/Memoria.EventSourcing.Store.Cosmos) | [![NuGet](https://img.shields.io/nuget/v/Memoria.EventSourcing.Store.Cosmos)](https://www.nuget.org/packages/Memoria.EventSourcing.Store.Cosmos) | Azure Cosmos DB store (SQL API) |
| [Memoria.EventSourcing.Store.Cosmos.InMemory](https://www.nuget.org/packages/Memoria.EventSourcing.Store.Cosmos.InMemory) | [![NuGet](https://img.shields.io/nuget/v/Memoria.EventSourcing.Store.Cosmos.InMemory)](https://www.nuget.org/packages/Memoria.EventSourcing.Store.Cosmos.InMemory) | In-process Cosmos DB stand-in for tests |
| [Memoria.Messaging.ServiceBus](https://www.nuget.org/packages/Memoria.Messaging.ServiceBus) | [![NuGet](https://img.shields.io/nuget/v/Memoria.Messaging.ServiceBus)](https://www.nuget.org/packages/Memoria.Messaging.ServiceBus) | Publish to Azure Service Bus when a command succeeds |
| [Memoria.Messaging.ServiceBus.InMemory](https://www.nuget.org/packages/Memoria.Messaging.ServiceBus.InMemory) | [![NuGet](https://img.shields.io/nuget/v/Memoria.Messaging.ServiceBus.InMemory)](https://www.nuget.org/packages/Memoria.Messaging.ServiceBus.InMemory) | In-process Service Bus stand-in for tests |
| [Memoria.Messaging.RabbitMq](https://www.nuget.org/packages/Memoria.Messaging.RabbitMq) | [![NuGet](https://img.shields.io/nuget/v/Memoria.Messaging.RabbitMq)](https://www.nuget.org/packages/Memoria.Messaging.RabbitMq) | Publish to RabbitMQ when a command succeeds |
| [Memoria.Messaging.RabbitMq.InMemory](https://www.nuget.org/packages/Memoria.Messaging.RabbitMq.InMemory) | [![NuGet](https://img.shields.io/nuget/v/Memoria.Messaging.RabbitMq.InMemory)](https://www.nuget.org/packages/Memoria.Messaging.RabbitMq.InMemory) | In-process RabbitMQ stand-in for tests |
| [Memoria.Caching.Memory](https://www.nuget.org/packages/Memoria.Caching.Memory) | [![NuGet](https://img.shields.io/nuget/v/Memoria.Caching.Memory)](https://www.nuget.org/packages/Memoria.Caching.Memory) | Cache query results in-process |
| [Memoria.Caching.Redis](https://www.nuget.org/packages/Memoria.Caching.Redis) | [![NuGet](https://img.shields.io/nuget/v/Memoria.Caching.Redis)](https://www.nuget.org/packages/Memoria.Caching.Redis) | Cache query results in Redis |
| [Memoria.Validation.FluentValidation](https://www.nuget.org/packages/Memoria.Validation.FluentValidation) | [![NuGet](https://img.shields.io/nuget/v/Memoria.Validation.FluentValidation)](https://www.nuget.org/packages/Memoria.Validation.FluentValidation) | Validate commands before they reach the handler |

## 🗺️ Roadmap

Shipped work is in the [release notes](https://lucabriguglia.github.io/Memoria/release-notes.html).
Next up, in no fixed order:

- Pipelines
- Various improvements to Memoria Web
- Option to automatically validate commands
- Event Grid messaging provider
- Kafka messaging provider
- Amazon SQS messaging provider
- File store provider for event sourcing

## 🤝 Contributing

Bug reports, questions and feature requests are welcome as
[issues](https://github.com/lucabriguglia/Memoria/issues) and
[discussions](https://github.com/lucabriguglia/Memoria/discussions). For code, please open an issue
first, and read [CONTRIBUTING.md](https://github.com/lucabriguglia/Memoria/blob/main/CONTRIBUTING.md)
— it covers how to build and test, the conventions to follow, and the
[contributor licence agreement](https://github.com/lucabriguglia/Memoria/blob/main/CLA.md) that
dual licensing makes necessary. You keep the copyright in your work.

By taking part you agree to the [Code of Conduct](https://github.com/lucabriguglia/Memoria/blob/main/CODE_OF_CONDUCT.md).

## ⭐ Give a star

If Memoria is useful in your learning, samples, workshop or project, please give the repository a
star. Thank you!

## ✨ Custom implementations and project support

Memoria is designed to be extended, with providers for store, messaging, caching and validation.

Need a provider that does not exist yet — a custom database store, a different message bus — or help
applying Memoria to an existing codebase? Get in touch via
[LinkedIn](https://www.linkedin.com/in/lucabriguglia).

## 📄 Licence

Memoria is dual-licensed from version 2.0.0-beta onward. Every 2.x version — alpha, beta and release
alike — is offered under **either** of:

- the **[Reciprocal Public License 1.5](https://opensource.org/license/rpl-1-5)**, an OSI-approved
  open-source licence, if you release the source of the software you build with Memoria under the
  same licence. Unlike most copyleft licences, that condition also applies to software you deploy
  for others to use without distributing it, such as a web application or a hosted service.
- the **[Memoria Commercial Licence](https://lucabriguglia.github.io/Memoria/license.html)**, if you
  do not.

The commercial licence's **Community** edition is free of charge, and always will be, for companies
and individuals with less than $5,000,000 USD in annual gross revenue and for registered non-profits
with less than $5,000,000 USD in annual total budget. Government and quasi-government agencies do
not qualify, and neither does any organisation that has ever received more than $10,000,000 USD in
outside capital such as private equity or venture capital.

The **Standard**, **Professional** and **Enterprise** editions are subscriptions at $299, $999 and
$2,999 USD a year, or $29.90, $99.90 and $299.90 USD a month — half price for anyone who subscribes
while 2.0.0 is in beta.

Versions 1.x remain under the
[Apache License 2.0](https://github.com/lucabriguglia/Memoria/blob/1.9.1/LICENSE). The full terms
are in [LICENSE.md](https://github.com/lucabriguglia/Memoria/blob/main/LICENSE.md) and on the
[licence page](https://lucabriguglia.github.io/Memoria/license.html).
