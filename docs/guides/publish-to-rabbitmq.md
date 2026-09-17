---
title: Publish to RabbitMQ
description: "Publish in-process notifications and RabbitMQ messages when a command handler succeeds, and read the result of each."
parent: Guides
nav_order: 5
---

# Publish notifications and messages to RabbitMQ

When a command handler succeeds, Memoria can publish in-process **notifications** to every
`INotificationHandler<>` and **messages** to RabbitMQ. A failing handler publishes neither, so a
downstream consumer never sees a side effect for work that did not happen.

The mechanism is the same across every messaging provider — only the registration differs. If you
have read [Publish to Service Bus](publish-to-service-bus.md), the shape below will be familiar.

This guide assumes a RabbitMQ provider is registered — see
[Configuration: RabbitMQ](../reference/configuration/messaging-rabbitmq.md).

## Register the provider

```csharp
services.AddMemoria(typeof(Program));
services.AddMemoriaRabbitMq("amqp://guest:guest@localhost:5672/");
```

There is an options overload when you want to set more than the connection string:

```csharp
services.AddMemoriaRabbitMq(options =>
{
    options.ConnectionString = connectionString;
});
```

For tests and local development without a broker, register
`Memoria.Messaging.RabbitMq.InMemory` instead — see
[Test without external dependencies](test-without-external-deps.md).

## Define the message

A message is a class deriving from `QueueMessage` or `TopicMessage`, both in `Memoria.Messaging`.
The base supplies the routing name and the bus metadata; the properties you add are the payload.

```csharp
using Memoria.Messaging;

public class OrderPlacedMessage : QueueMessage
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
}
```

`QueueName` is `required`, so set it where you create the message. `TopicMessage` carries
`TopicName` in its place. Both inherit `ScheduledEnqueueTimeUtc`, for a message that should not be
delivered until a given moment, and a `Properties` dictionary that travels with it.

## Return it from the handler

The handler returns a `CommandResponse` carrying the notifications, the messages, and whatever the
caller should get back.

```csharp
public record PlaceOrder(Guid OrderId, decimal Amount) : ICommand<CommandResponse>;

public class PlaceOrderHandler : ICommandHandler<PlaceOrder, CommandResponse>
{
    public Task<Result<CommandResponse>> Handle(
        PlaceOrder command,
        CancellationToken cancellationToken = default)
    {
        // The order is placed here.

        var response = new CommandResponse
        {
            Messages =
            [
                new OrderPlacedMessage
                {
                    QueueName = "orders-placed",
                    OrderId = command.OrderId,
                    Amount = command.Amount
                }
            ]
        };

        return Task.FromResult(Result.Ok(response));
    }
}
```

The queue is declared durable on first use, so it does not have to exist beforehand.

## Dispatch with `SendAndPublish`

`Send` runs the handler alone. `SendAndPublish` runs it and then publishes what the response carries:

```csharp
var response = await dispatcher.SendAndPublish(new PlaceOrder(orderId, 25.45m));
```

You get back the command's own result alongside one result per notification handler and one per
message:

```csharp
if (response.CommandResult.IsSuccess &&
    response.MessageResults.Any(m => m.IsNotSuccess))
{
    // the order was placed; at least one message did not reach the broker
}
```

Check `MessageResults`. **A message that cannot be delivered does not fail the command** — the
command already succeeded, and the two outcomes are reported separately so you can decide what a
failed publish means for you.

## Where a broken connection shows up

From 2.0.0 the provider opens its connection on the **first send**, not when it is constructed,
because RabbitMQ.Client 7 connects asynchronously and a constructor cannot await. An unreachable
broker is therefore reported as that send's `Failure` rather than thrown where the provider is
resolved. If you upgraded from 1.9.x and expected a bad connection string to stop the host at
start-up, see [Upgrade to 2.0.0](upgrade-2.0.0.md#rabbitmq).

## Related

- [Configuration: RabbitMQ](../reference/configuration/messaging-rabbitmq.md)
- [Publish to Service Bus](publish-to-service-bus.md) — the same pattern, on Azure Service Bus
- [Test without external dependencies](test-without-external-deps.md)
- [Upgrade to 2.0.0](upgrade-2.0.0.md)
