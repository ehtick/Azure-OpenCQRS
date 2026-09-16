using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// The tests of what is exported, run while nothing else is.
/// </summary>
/// <remarks>
/// OpenTelemetry listens to ASP.NET Core's requests process-wide, not per host: an instance told
/// where to send telemetry sees every request every other instance in the process answers while
/// it is up, and enriches each with that request's own operator. Run in parallel with the rest,
/// its exported requests are mostly other tests', signed in as somebody else or as nobody. They
/// are collected here, and the collection told not to run alongside any other, so what an
/// instance exports is what it was asked. It costs a few seconds and removes a failure that says
/// nothing about the code.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public class TelemetryCollection
{
    public const string Name = "Telemetry";
}
