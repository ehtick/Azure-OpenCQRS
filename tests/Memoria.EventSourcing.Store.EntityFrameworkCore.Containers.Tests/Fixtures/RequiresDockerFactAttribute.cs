using Xunit;

namespace Memoria.EventSourcing.Store.EntityFrameworkCore.Containers.Tests.Fixtures;

/// <summary>
/// A <see cref="FactAttribute"/> that skips instead of failing when there is no Docker endpoint to
/// run containers against, so the suite stays runnable on machines without Docker.
/// </summary>
/// <remarks>
/// This covers Docker being absent. An endpoint that exists but cannot start the engine is left to
/// fail the test — that is a real problem, not a reason to quietly skip.
/// </remarks>
public sealed class RequiresDockerFactAttribute : FactAttribute
{
    public RequiresDockerFactAttribute()
    {
        if (DockerAvailability.UnavailableReason is { } reason)
        {
            Skip = reason;
        }
    }
}

internal static class DockerAvailability
{
    private const string UnixDockerSocket = "/var/run/docker.sock";

    // Docker Desktop exposes "docker_engine"; the WSL2 backend also exposes
    // "dockerDesktopLinuxEngine", and a context may point at either.
    private static readonly string[] WindowsDockerPipes =
    [
        @"\\.\pipe\docker_engine",
        @"\\.\pipe\dockerDesktopLinuxEngine"
    ];

    private static readonly Lazy<string?> Probe = new(FindDockerEndpoint, LazyThreadSafetyMode.ExecutionAndPublication);

    public static string? UnavailableReason => Probe.Value;

    private static string? FindDockerEndpoint()
    {
        // An explicit endpoint is taken at its word; if it is wrong the container start will say so.
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST")))
        {
            return null;
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                return WindowsDockerPipes.Any(File.Exists)
                    ? null
                    : "Docker is not available (no Docker named pipe found and DOCKER_HOST is unset). Is Docker Desktop running?";
            }

            return File.Exists(UnixDockerSocket)
                ? null
                : $"Docker is not available (no socket at '{UnixDockerSocket}' and DOCKER_HOST is unset).";
        }
        catch (Exception exception)
        {
            return $"Docker availability could not be determined ({exception.GetType().Name}: {exception.Message}).";
        }
    }
}
