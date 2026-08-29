using FluentAssertions;
using Xunit;

namespace Memoria.EventSourcing.Tests.Features;

/// <summary>
/// The serializer is a replaceable seam, but Newtonsoft must stay the default: every store already
/// written was written by it, and swapping implementations is only safe for a consumer who has
/// checked their own models.
/// </summary>
public class DomainSerializerTests : IDisposable
{
    private readonly IDomainSerializer _original = DomainSerializer.Current;

    private sealed class ReversingSerializer : IDomainSerializer
    {
        public string Serialize(object value) => "serialized";

        public object Deserialize(string json, Type type) => "deserialized";
    }

    private record Payload(string Name, int Count);

    [Fact]
    public void TheDefaultIsNewtonsoft() =>
        DomainSerializer.Current.Should().BeOfType<NewtonsoftDomainSerializer>();

    [Fact]
    public void TheDefaultRoundTripsAPayload()
    {
        var json = DomainSerializer.Current.Serialize(new Payload("test", 3));

        var restored = DomainSerializer.Current.Deserialize(json, typeof(Payload));

        restored.Should().BeEquivalentTo(new Payload("test", 3));
    }

    [Fact]
    public void TheDefaultWritesToPrivateSetters()
    {
        // Aggregates and projections keep their state private, so reading one back has to reach
        // non-public setters. Serialization deliberately does not use the same settings.
        var restored = (WithPrivateSetter)DomainSerializer.Current
            .Deserialize("""{"Name":"set"}""", typeof(WithPrivateSetter));

        restored.Name.Should().Be("set");
    }

    [Fact]
    public void TheSerializerCanBeReplaced()
    {
        DomainSerializer.Current = new ReversingSerializer();

        DomainSerializer.Current.Serialize(new Payload("ignored", 0)).Should().Be("serialized");
        DomainSerializer.Current.Deserialize("{}", typeof(Payload)).Should().Be("deserialized");
    }

    public void Dispose() => DomainSerializer.Current = _original;

    private class WithPrivateSetter
    {
        public string Name { get; private set; } = null!;
    }
}
