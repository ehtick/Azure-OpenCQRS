using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using BenchmarkDotNet.Attributes;
using Memoria.EventSourcing;
using Newtonsoft.Json;
using JsonIgnoreAttribute = Newtonsoft.Json.JsonIgnoreAttribute;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Memoria.Benchmarks;

/// <summary>
/// Sizes the prize before reshaping how the store serializes: how much faster System.Text.Json
/// actually is on the payloads the event store writes and reads, against the Newtonsoft
/// configuration the store uses today.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class SerializerBenchmarks
{
    private static readonly JsonSerializerSettings NewtonsoftSettings = new()
    {
        ContractResolver = new PrivateSetterContractResolver()
    };

    /// <summary>
    /// What a System.Text.Json implementation would need to match the store's current behaviour:
    /// write to non-public setters, and honour the Newtonsoft [JsonIgnore] already on the base
    /// classes and, quite possibly, on consumer models.
    /// </summary>
    private static readonly JsonSerializerOptions SystemTextJsonOptions = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { HonourNewtonsoftIgnore, AllowNonPublicSetters }
        }
    };

    private OrderPlacedEvent @event = null!;
    private OrderAggregate aggregate = null!;
    private string eventJsonNewtonsoft = null!;
    private string aggregateJsonNewtonsoft = null!;
    private string eventJsonSystemTextJson = null!;
    private string aggregateJsonSystemTextJson = null!;

    [GlobalSetup]
    public void Setup()
    {
        @event = new OrderPlacedEvent(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), 199.99m,
            DateTimeOffset.UtcNow);

        aggregate = new OrderAggregate();
        aggregate.Apply([@event]);

        eventJsonNewtonsoft = JsonConvert.SerializeObject(@event);
        aggregateJsonNewtonsoft = JsonConvert.SerializeObject(aggregate);
        eventJsonSystemTextJson = JsonSerializer.Serialize(@event, SystemTextJsonOptions);
        aggregateJsonSystemTextJson = JsonSerializer.Serialize(aggregate, SystemTextJsonOptions);
    }

    [Benchmark(Baseline = true, Description = "Event serialize (Newtonsoft)")]
    public string EventSerializeNewtonsoft() => JsonConvert.SerializeObject(@event);

    [Benchmark(Description = "Event serialize (System.Text.Json)")]
    public string EventSerializeSystemTextJson() => JsonSerializer.Serialize(@event, SystemTextJsonOptions);

    [Benchmark(Description = "Event deserialize (Newtonsoft)")]
    public object EventDeserializeNewtonsoft() =>
        JsonConvert.DeserializeObject(eventJsonNewtonsoft, typeof(OrderPlacedEvent), NewtonsoftSettings)!;

    [Benchmark(Description = "Event deserialize (System.Text.Json)")]
    public object EventDeserializeSystemTextJson() =>
        JsonSerializer.Deserialize(eventJsonSystemTextJson, typeof(OrderPlacedEvent), SystemTextJsonOptions)!;

    [Benchmark(Description = "Aggregate serialize (Newtonsoft)")]
    public string AggregateSerializeNewtonsoft() => JsonConvert.SerializeObject(aggregate);

    [Benchmark(Description = "Aggregate serialize (System.Text.Json)")]
    public string AggregateSerializeSystemTextJson() => JsonSerializer.Serialize(aggregate, SystemTextJsonOptions);

    [Benchmark(Description = "Aggregate deserialize (Newtonsoft)")]
    public object AggregateDeserializeNewtonsoft() =>
        JsonConvert.DeserializeObject(aggregateJsonNewtonsoft, typeof(OrderAggregate), NewtonsoftSettings)!;

    [Benchmark(Description = "Aggregate deserialize (System.Text.Json)")]
    public object AggregateDeserializeSystemTextJson() =>
        JsonSerializer.Deserialize(aggregateJsonSystemTextJson, typeof(OrderAggregate), SystemTextJsonOptions)!;

    private static void AllowNonPublicSetters(JsonTypeInfo typeInfo)
    {
        foreach (var property in typeInfo.Properties)
        {
            if (property.Set is not null || property.AttributeProvider is not PropertyInfo propertyInfo)
            {
                continue;
            }

            var setter = propertyInfo.GetSetMethod(nonPublic: true);

            if (setter is not null)
            {
                property.Set = (target, value) => setter.Invoke(target, [value]);
            }
        }
    }

    private static void HonourNewtonsoftIgnore(JsonTypeInfo typeInfo)
    {
        for (var index = typeInfo.Properties.Count - 1; index >= 0; index--)
        {
            if (typeInfo.Properties[index].AttributeProvider is PropertyInfo property && IsIgnored(property))
            {
                typeInfo.Properties.RemoveAt(index);
            }
        }
    }

    /// <summary>
    /// Walks the override chain by hand. .NET does not inherit attributes on overridden properties —
    /// <c>GetCustomAttributes(inherit: true)</c> returns nothing for a property whose attribute sits
    /// on the base declaration — so an attribute like the [JsonIgnore] on EventSourcedModel is
    /// invisible to a naive lookup. Newtonsoft does this walk internally.
    /// </summary>
    private static bool IsIgnored(PropertyInfo property)
    {
        const BindingFlags declared = BindingFlags.Public | BindingFlags.NonPublic
                                                          | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        for (var type = property.DeclaringType; type is not null; type = type.BaseType)
        {
            var declaration = type.GetProperty(property.Name, declared);

            if (declaration?.IsDefined(typeof(JsonIgnoreAttribute), inherit: false) == true)
            {
                return true;
            }
        }

        return false;
    }
}
