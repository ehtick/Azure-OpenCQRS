using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using Xunit;
using static Memoria.Web.Samples.Seeding.SampleVocabulary;

namespace Memoria.Web.Samples.Tests.Features;

/// <summary>
/// The identifiers a run writes its sample data under.
/// </summary>
/// <remarks>
/// Every one of them is a key in the store — an aggregate is saved under its identifier, and the
/// second row under one identifier is rejected by the primary key, not merged into the first. So
/// the thing worth pinning down is that no run issues the same identifier twice, however many it
/// issues: an identifier drawn at random collides long before a store is large, and a run that
/// wrote five hundred of anything found one.
/// </remarks>
public class SampleIdentifierTests
{
    /// <summary>
    /// Far more than any run writes, because the collision this is about is one of chance: drawing
    /// a few and finding them different proves nothing at all.
    /// </summary>
    private const int More = 200_000;

    [Fact]
    public void Issues_no_identifier_twice()
    {
        var identifiers = Enumerable.Range(0, More).Select(_ => Id("c")).ToList();

        identifiers.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Issues_no_stock_keeping_unit_twice()
    {
        var units = Enumerable.Range(0, More).Select(_ => Sku("Walnut Desk")).ToList();

        units.Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// Identifiers of different kinds never meet in one column here, but they are read side by
    /// side by whoever is using the tool, and one value meaning two things is a confusion nobody
    /// needs.
    /// </summary>
    [Fact]
    public void Keeps_the_kinds_apart()
    {
        var identifiers = new List<string>();

        for (var index = 0; index < More / 4; index++)
        {
            identifiers.Add(Id("c"));
            identifiers.Add(Id("o"));
        }

        identifiers.Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// The point of the identifier: it is read off the run's own report and typed into the tool.
    /// </summary>
    [Fact]
    public void Keeps_an_identifier_short_enough_to_retype()
    {
        Id("c").Length.Should().BeLessThanOrEqualTo(14);
    }

    /// <summary>
    /// A run adds to a store an earlier run wrote, so uniqueness within a run is not enough: two
    /// runs must not issue the same identifiers either. What keeps them apart is the moment each
    /// one started.
    /// </summary>
    [Fact]
    public void Marks_a_run_apart_from_one_that_started_at_another_moment()
    {
        var moment = DateTimeOffset.UtcNow;

        Run(moment, 0).Should().NotBe(Run(moment.AddSeconds(1), 0));
        Run(moment, 0).Should().NotBe(Run(moment, 1));
    }
}
