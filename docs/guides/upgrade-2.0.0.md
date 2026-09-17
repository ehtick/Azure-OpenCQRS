---
title: Upgrade to 2.0.0
parent: Upgrading
nav_order: 1
---

# Upgrade to 2.0.0
{: .no_toc }

## On this page
{: .no_toc .text-delta }

1. TOC
{:toc}

The major version steps to 2 for the licence, not for the API. Almost nothing in the code you have
written against 1.9.x has to change, and no table, document or serialised payload changes, so there
is no data migration.

What does change is the terms you use Memoria under, and you have to choose. Two client libraries
also step a major version, and one of them moves a behaviour you may be relying on.

<a name="choose-a-licence"></a>
## 1. Choose a licence

This is the one step that applies to everybody.

Memoria 1.x was released under the Apache License 2.0 and stays there. **A version is licensed under
the terms it shipped with**, so nothing about any 1.x package you already use changes, ever. From
2.0.0-beta onward, every 2.x version is offered under **either** of two licences, and you pick the
one you use it under:

| Your situation | Licence | Cost |
|----------------|---------|------|
| You release the source of what you build under the same licence | [Reciprocal Public License 1.5](https://opensource.org/license/rpl-1-5) | Free |
| Closed source, under $5,000,000 USD annual revenue, or a non-profit under $5,000,000 USD annual budget | [Memoria Commercial Licence](../license.md), **Community** edition | Free, and always will be |
| Closed source, above that threshold | Memoria Commercial Licence, Standard / Professional / Enterprise | From $299 USD a year |

The RPL is stricter than the licences most .NET libraries carry, and the difference matters: unlike
the GPL, its reciprocity is triggered by **deploying** software for others to use, not only by
distributing it. A hosted service or an internal line-of-business application built on Memoria must
publish its source under the RPL just as a shipped product must. If that is not what you want, the
Commercial Licence is the alternative, and its Community edition covers most small companies at no
charge.

Read the [licence page](../license.md) before upgrading, including the Community eligibility terms —
government and quasi-government agencies do not qualify, and neither does an organisation that has
ever taken more than $10,000,000 USD in outside capital.

The packages themselves changed with it: they now carry `LICENSE.md` in place of an SPDX expression,
and they ask for the licence to be accepted on install.

**If neither licence suits you, stay on 1.9.1.** It remains under Apache 2.0 and is not going
anywhere.

<a name="rabbitmq"></a>
## 2. If you publish to RabbitMQ, a connection failure now surfaces later

`Memoria.Messaging.RabbitMq` moves to RabbitMQ.Client 7, whose API is asynchronous throughout. A
constructor cannot await, so **the provider opens its connection on the first send rather than when
it is constructed**.

The practical difference is where a bad connection string shows up:

| | 1.9.x | 2.0.0 |
|---|---|---|
| Broker unreachable | Throws where the provider is resolved, typically at start-up | Returned as that send's `Failure`, like any other send error |

If you were relying on the old timing to fail fast at start-up — a health check that resolved
`IMessagingProvider`, or a smoke test that expected the host to refuse to start against a bad broker
— it no longer fails there. The failure is real either way; it arrives at the send instead. Check
the `MessageResults` on the `SendAndPublish` response, which you should be doing regardless:

```csharp
var response = await dispatcher.SendAndPublish(new PlaceOrder(orderId, amount));

if (response.MessageResults.Any(m => m.IsNotSuccess))
{
    // the command succeeded; the message did not reach the broker
}
```

A custom `IConnection` handed to the provider through its second constructor is used exactly as
before.

<a name="redis"></a>
## 3. If you cache in Redis, nothing to do

`Memoria.Caching.Redis` moves to StackExchange.Redis 3, which speaks RESP3 to a server that offers
it and RESP2 to one that does not. Nothing in the provider's use of the client changes, and nothing
in yours has to.

<a name="memoria-web"></a>
## 4. If you use Memoria Web, your zips need a manifest

[Memoria Web](../tools/memoria-web.md) changed more than the library did.

- **An uploaded zip must now carry a `memoria.json` at its root**, declaring the services the archive
  brings: for each, a display name, the assembly files its domain types are read from, the name of
  the connection string it is read over, and optionally the claim values that may read and update it.
  An archive without one is rejected. See
  [Configuration](../tools/memoria-web-configuration.md) for the manifest's shape.
- **Every page that reads a store moved under its service's name** — `/orders/streamed/events`,
  `/orders/dcb/aggregates/data`. Bookmarked URLs from an earlier version will not resolve.
- One instance can now hold several services, each reading the store its manifest names.

<a name="type-bindings"></a>
## 5. Optional: give a store its own type bindings

Until now, event and aggregate keys — `OrderPlaced:1`, `Order:1` — resolved to CLR types through one
set of bindings per process, the static maps on `TypeBindings` and `DcbTypeBindings`. A host reading
two bounded contexts that both declare an `OrderPlaced` at version 1 could not bind both.

Those maps now live on `TypeBindingSet`, and the statics are views over the process-wide
`TypeBindingSet.Default`. **An application that never mentions `TypeBindingSet` binds exactly as it
did before** — this is an addition, not a change. Reach for it only when one process genuinely has to
read more than one context's store.

## Related

- [Licence](../license.md) — the full terms of both licences, and the Community eligibility rules
- [Release notes](../release-notes.md) — everything in 2.0.0-beta, not only what needs action
- [Publish to RabbitMQ](publish-to-rabbitmq.md)
- [Memoria Web](../tools/memoria-web.md)
