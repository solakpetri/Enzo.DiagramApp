using Enzo.Diagrams.Application;
using Enzo.Diagrams.Cli;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Enzo.Diagrams.Cli.Tests;

public sealed class CliCompositionTests
{
    [Fact]
    public void Composition_ResolvesApplicationAndInfrastructureServices()
    {
        using var services = CliComposition.CreateServices();

        Assert.NotNull(services.GetRequiredService<DiagramService>());
        Assert.NotNull(services.GetRequiredService<IDiagramRenderer>());
    }
}
