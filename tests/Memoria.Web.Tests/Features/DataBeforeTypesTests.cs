using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// Every section offers what has been stored before what the assemblies declare: Data first, then
/// Types, on the section's own tiles and in every menu that leads to them. What is stored is what a
/// reader most often comes for; the declarations are what they turn to once a row needs explaining.
/// </summary>
public class DataBeforeTypesTests
{
    [Theory]
    [InlineData("streamed/events")]
    [InlineData("streamed/aggregates")]
    [InlineData("streamed/projections")]
    [InlineData("dcb/events")]
    [InlineData("dcb/aggregates")]
    [InlineData("dcb/projections")]
    public async Task Offers_the_data_tile_before_the_types_tile(string section)
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var tiles = Main(Markup.Plain(await web.Client.GetStringAsync($"/samples/{section}")));

        Place(tiles, $"samples/{section}/data").Should().BeLessThan(Place(tiles, $"samples/{section}/types"));
    }

    /// <summary>Under each model's heading, every section's group lists its overview, then Data, then Types.</summary>
    [Fact]
    public async Task Lists_data_before_types_in_every_group_under_the_model_headings()
    {
        using var web = MemoriaWeb.Open().WithSampleTypes();

        var header = Markup.Header(await web.Client.GetStringAsync("/samples"));

        using var scope = new AssertionScope();

        foreach (var section in new[]
                 {
                     "streamed/events", "streamed/aggregates", "streamed/projections",
                     "dcb/events", "dcb/aggregates", "dcb/projections"
                 })
        {
            Place(header, $"samples/{section}/data").Should().BeLessThan(Place(header, $"samples/{section}/types"), section);
        }
    }

    /// <summary>With one model laid out alone, each section is a heading of its own, listed the same way.</summary>
    [Fact]
    public async Task Lists_data_before_types_under_each_section_heading_of_a_model_laid_out_alone()
    {
        using var web = MemoriaWeb.Open().WithDcbTypesOnly();

        var header = Markup.Header(await web.Client.GetStringAsync("/samples"));

        using var scope = new AssertionScope();

        foreach (var section in new[] { "dcb/events", "dcb/aggregates", "dcb/projections" })
        {
            Place(header, $"samples/{section}/data").Should().BeLessThan(Place(header, $"samples/{section}/types"), section);
        }
    }

    /// <summary>The page's own content, without the header's menus, which link to both pages as well.</summary>
    private static string Main(string page)
    {
        var start = page.IndexOf("<main", StringComparison.Ordinal);

        return start < 0 ? string.Empty : page[start..];
    }

    /// <summary>Where the first link to an address is, failing the test when there is none.</summary>
    private static int Place(string markup, string href)
    {
        var place = markup.IndexOf($"href=\"{href}\"", StringComparison.Ordinal);
        place.Should().BeGreaterThanOrEqualTo(0, $"a link to {href} is expected");

        return place;
    }
}
