---
title: Publish to Service Bus
description: "Publish in-process notifications and Azure Service Bus messages when a command handler succeeds, and read the result of each."
parent: Guides
nav_order: 4
---

# Publish notifications and messages on command success

Memoria can automatically publish in-process **notifications** and bus **messages** when a command handler succeeds. This is how you keep side effects (emails, search indexing, downstream services) decoupled from the command's primary work.

This guide focuses on Azure Service Bus. The pattern is identical for [RabbitMQ](publish-to-rabbitmq.md).

This guide assumes you have a messaging provider registered — see [Configuration: Service Bus](../reference/configuration/messaging-servicebus.md).

## The shape

A command that opts in returns a `CommandResponse` carrying:

1. **Notifications** — published in-process to every `INotificationHandler<>`.
2. **Messages** — published to the configured bus (Service Bus, RabbitMQ, or any custom provider).
3. **Result value** — the data returned to the caller.

## Define the contracts

```csharp
using Memoria.Messaging;

public record DoSomething(string Name) : ICommand<CommandResponse>;
public record SomethingHappened(string Name) : INotification;

public class SomethingToSendToServiceBus : QueueMessage
{
    public string Name { get; set; } = string.Empty;
}
```

A notification is any record implementing `INotification`. A message must derive from
`QueueMessage` or `TopicMessage`, whose `QueueName` and `TopicName` are `required` and say where it
is published.

## Implement the handlers

```csharp
public class DoSomethingHandler : ICommandHandler<DoSomething, CommandResponse>
{
    public Task<Result<CommandResponse>> Handle(
        DoSomething command,
        CancellationToken cancellationToken = default)
    {
        var notification = new SomethingHappened(command.Name);
        var message = new SomethingToSendToServiceBus
        {
            QueueName = "something-happened",
            Name = command.Name
        };

        var response = new CommandResponse(
            notification,
            message,
            new { Message = $"Successfully processed command for: {command.Name}" });

        return Task.FromResult(Result.Ok(response));
    }
}

public class SomethingHappenedHandlerOne : INotificationHandler<SomethingHappened>
{
    public Task<Result> Handle(SomethingHappened notification, CancellationToken _ = default)
    {
        Console.WriteLine($"Handler One: {notification.Name}");
        return Task.FromResult(Result.Ok());
    }
}

public class SomethingHappenedHandlerTwo : INotificationHandler<SomethingHappened>
{
    public Task<Result> Handle(SomethingHappened notification, CancellationToken _ = default)
    {
        Console.WriteLine($"Handler Two: {notification.Name}");
        return Task.FromResult(Result.Ok());
    }
}
```

## Dispatch

Use `SendAndPublish` instead of `Send`:

```csharp
var result = await dispatcher.SendAndPublish(new DoSomething("MyName"));
```

The returned object contains the command's result plus the result of every notification and message handler:

```csharp
{
    "CommandResult": {
        "IsSuccess": true,
        "Value": { "Message": "Successfully processed command for: MyName" }
    },
    "NotificationResults": [
        { "IsSuccess": true },
        { "IsSuccess": true }
    ],
    "MessageResults": [
        { "IsSuccess": true }
    ]
}
```

## What "on success" means

Notifications and messages are only published if the command handler returns a success result. A failure short-circuits — the original command failure is returned, no notifications fire, no messages reach the bus. This guarantees that downstream consumers never see a side effect for work that didn't happen.

## Related

- [Quickstart: Mediator](../getting-started/quickstart-mediator.md)
- [Configuration: Service Bus](../reference/configuration/messaging-servicebus.md)
- [Publish to RabbitMQ](publish-to-rabbitmq.md)
