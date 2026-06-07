using System.Runtime.InteropServices;

namespace PaymentLedger.IntegrationTests.Infrastructure;

public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (!IsDockerAvailable())
        {
            Skip = "Docker is unavailable in this environment, so the real PostgreSQL bootstrap test is skipped.";
        }
    }

    private static bool IsDockerAvailable()
    {
        var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");

        if (!string.IsNullOrWhiteSpace(dockerHost))
        {
            return true;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return File.Exists(@"\\.\pipe\docker_engine");
        }

        return File.Exists("/var/run/docker.sock");
    }
}
