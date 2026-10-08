using Xunit.Abstractions;
using Xunit.Sdk;

namespace ArcaSim.Tests.Support;

/// <summary>Marks a test class whose tests need Docker: <see cref="DockerFactAttribute"/> skips them, instead of failing them, when it is not reachable.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RequiresDockerAttribute : Attribute;

/// <summary>
/// A fact that runs everywhere except in a test class marked <see cref="RequiresDockerAttribute"/> on a
/// machine without Docker, where it is reported as skipped. The class decides, so a contract written once
/// in a base class can run against an in-memory store and against PostgreSQL. CI sets
/// ARCASIM_REQUIRE_DOCKER=1, which turns the skip into a failure.
/// </summary>
[XunitTestCaseDiscoverer("ArcaSim.Tests.Support.DockerFactDiscoverer", "ArcaSim.Tests")]
[AttributeUsage(AttributeTargets.Method)]
public sealed class DockerFactAttribute : FactAttribute;

public sealed class DockerFactDiscoverer(IMessageSink diagnosticMessageSink) : IXunitTestCaseDiscoverer
{
    public IEnumerable<IXunitTestCase> Discover(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo factAttribute)
    {
        var display = discoveryOptions.MethodDisplayOrDefault();
        var displayOptions = discoveryOptions.MethodDisplayOptionsOrDefault();
        var needsDocker = testMethod.TestClass.Class.GetCustomAttributes(typeof(RequiresDockerAttribute).AssemblyQualifiedName!).Any();
        yield return needsDocker && DockerAvailability.SkipReason is { } reason
            ? new XunitSkippedDataRowTestCase(diagnosticMessageSink, display, displayOptions, testMethod, reason)
            : new XunitTestCase(diagnosticMessageSink, display, displayOptions, testMethod);
    }
}
