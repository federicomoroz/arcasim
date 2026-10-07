using DotNet.Testcontainers.Configurations;

namespace ArcaSim.Tests.Support;

/// <summary>Whether the tests that need Docker can run on this machine, decided once per run.</summary>
internal static class DockerAvailability
{
    /// <summary>The variable CI sets to 1: with it, a Docker that is not there fails the tests that need it instead of skipping them.</summary>
    public const string RequireVariable = "ARCASIM_REQUIRE_DOCKER";

    private static readonly Lazy<string?> Unreachable = new(Probe);

    public static bool Required { get; } =
        Environment.GetEnvironmentVariable(RequireVariable) is { } value && (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase));

    /// <summary>Why the tests that need Docker are skipped, or null when they run (Docker answers, or it is required).</summary>
    public static string? SkipReason =>
        Required || Unreachable.Value is not { } why ? null : $"Docker is not reachable ({why}). Set {RequireVariable}=1 to fail instead of skipping.";

    /// <summary>Asks the Docker daemon Testcontainers would use whether it is there; null when it answers, what went wrong when not.</summary>
    private static string? Probe()
    {
        try
        {
            // Testcontainers settles on an endpoint that answers; with none that does, it has no configuration to give.
            if (TestcontainersSettings.OS?.DockerEndpointAuthConfig is not { } endpoint) return "no Docker daemon answers";
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var client = endpoint.GetDockerClientConfiguration(Guid.Empty).CreateClient();
            client.System.PingAsync(timeout.Token).GetAwaiter().GetResult();
            return null;
        }
        catch (Exception ex)
        {
            return ex.GetBaseException().Message;
        }
    }
}
