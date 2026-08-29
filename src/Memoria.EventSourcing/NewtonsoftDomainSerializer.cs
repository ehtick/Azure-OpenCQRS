using Newtonsoft.Json;

namespace Memoria.EventSourcing;

/// <summary>
/// The default <see cref="IDomainSerializer"/>, and the one every store provider has always used.
/// </summary>
/// <remarks>
/// Note the asymmetry, which is deliberate and preserved from the original code: writing uses
/// Newtonsoft's default settings, while reading adds <see cref="PrivateSetterContractResolver"/> so
/// that aggregates and projections with private setters can be rebuilt. Changing either side would
/// change the bytes written to, or the state read back from, every existing store.
/// </remarks>
public sealed class NewtonsoftDomainSerializer : IDomainSerializer
{
    private static readonly JsonSerializerSettings DeserializeSettings = new()
    {
        ContractResolver = new PrivateSetterContractResolver()
    };

    /// <inheritdoc />
    public string Serialize(object value) => 
        JsonConvert.SerializeObject(value);

    /// <inheritdoc />
    public object Deserialize(string json, Type type) =>
        JsonConvert.DeserializeObject(json, type, DeserializeSettings)!;
}
