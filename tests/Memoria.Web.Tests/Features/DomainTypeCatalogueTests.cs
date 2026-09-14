using FluentAssertions;
using Memoria.Web.Extensibility;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// What the catalogue says was registered from one assembly file — the list the settings page
/// unfolds under each archive.
/// </summary>
public class DomainTypeCatalogueTests
{
    private static readonly LoadedAssembly Samples = new("Samples.dll", typeof(SampleAggregate).Assembly);

    private static readonly LoadedAssembly Other = new("Other.dll", typeof(FluentActions).Assembly);

    private static DomainTypeCatalogue Catalogue() => new()
    {
        Assemblies = [Samples, Other],
        Events = [typeof(SampleHappenedEvent), typeof(SampleCarriedEvent)],
        StreamedEvents = [typeof(SampleHappenedEvent), typeof(SampleCarriedEvent)],
        DcbEvents = [typeof(SampleCarriedEvent)],
        StreamedAggregates = [typeof(SampleAggregate)],
        DcbProjectionIds = [typeof(SampleDcbProjectionId)]
    };

    /// <summary>
    /// In the order the Types tab counts them: one model's kinds, then the other's, so what a file
    /// brought each model is read as one block rather than picked out of a mixed list.
    /// </summary>
    [Fact]
    public void Lists_only_the_kinds_the_file_registered_something_under()
    {
        Catalogue().RegisteredFrom("Samples.dll").Select(kind => kind.Label)
            .Should().Equal("Streamed events", "Streamed aggregates", "DCB events", "DCB projection ids");
    }

    [Fact]
    public void Lists_the_types_under_their_kind()
    {
        Catalogue().RegisteredFrom("Samples.dll")
            .Should().Contain(kind => kind.Label == "Streamed events")
            .Which.Types.Should().Equal(typeof(SampleHappenedEvent), typeof(SampleCarriedEvent));
    }

    /// <summary>
    /// The events are listed as the event pages list them — the ones each model applies — rather
    /// than as the one bound set, so an event both models apply is under both.
    /// </summary>
    [Fact]
    public void Lists_the_events_each_model_applies()
    {
        Catalogue().RegisteredFrom("Samples.dll")
            .Should().Contain(kind => kind.Label == "DCB events")
            .Which.Types.Should().Equal(typeof(SampleCarriedEvent));
    }

    /// <summary>
    /// A type is listed under the file its own assembly was loaded from, not under every file.
    /// </summary>
    [Fact]
    public void Lists_nothing_under_a_file_whose_assembly_declares_none_of_the_types()
    {
        Catalogue().RegisteredFrom("Other.dll").Should().BeEmpty();
    }

    [Fact]
    public void Lists_nothing_under_a_file_that_was_not_loaded()
    {
        Catalogue().RegisteredFrom("Missing.dll").Should().BeEmpty();
    }

    /// <summary>
    /// The name comes from an archive entry on one side and a directory listing on the other, and
    /// neither is a place a difference of case means a different file.
    /// </summary>
    [Fact]
    public void Matches_the_file_name_regardless_of_case()
    {
        Catalogue().RegisteredFrom("SAMPLES.DLL").Should().NotBeEmpty();
    }

    [Fact]
    public void The_empty_catalogue_registered_nothing_from_anywhere()
    {
        DomainTypeCatalogue.Empty.RegisteredFrom("Samples.dll").Should().BeEmpty();
    }

    /// <summary>
    /// What the site narrows itself by: whether anything at all is registered under each model.
    /// An event alone counts for neither, since an event is the same event whichever model applies
    /// it and one no model applies belongs to neither.
    /// </summary>
    [Fact]
    public void The_empty_catalogue_has_neither_models_types()
    {
        DomainTypeCatalogue.Empty.HasStreamedTypes.Should().BeFalse();
        DomainTypeCatalogue.Empty.HasDcbTypes.Should().BeFalse();
    }

    [Fact]
    public void An_event_no_model_applies_belongs_to_neither()
    {
        var catalogue = new DomainTypeCatalogue { Events = [typeof(SampleHappenedEvent)] };

        catalogue.HasStreamedTypes.Should().BeFalse();
        catalogue.HasDcbTypes.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(StreamedKinds))]
    public void Any_streamed_kind_registered_means_the_streamed_model_has_types(DomainTypeCatalogue catalogue)
    {
        catalogue.HasStreamedTypes.Should().BeTrue();
        catalogue.HasDcbTypes.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(DcbKinds))]
    public void Any_dcb_kind_registered_means_the_dcb_model_has_types(DomainTypeCatalogue catalogue)
    {
        catalogue.HasDcbTypes.Should().BeTrue();
        catalogue.HasStreamedTypes.Should().BeFalse();
    }

    public static TheoryData<DomainTypeCatalogue> StreamedKinds => new(
        new DomainTypeCatalogue { StreamedStreamIds = [typeof(SampleStreamId)] },
        new DomainTypeCatalogue { StreamedAggregates = [typeof(SampleAggregate)] },
        new DomainTypeCatalogue { StreamedAggregateIds = [typeof(SampleAggregateId)] },
        new DomainTypeCatalogue { StreamedProjections = [typeof(SampleProjection)] },
        new DomainTypeCatalogue { StreamedProjectionIds = [typeof(SampleProjectionId)] },
        new DomainTypeCatalogue { Events = [typeof(SampleHappenedEvent)], StreamedEvents = [typeof(SampleHappenedEvent)] });

    public static TheoryData<DomainTypeCatalogue> DcbKinds => new(
        new DomainTypeCatalogue { DcbAggregates = [typeof(SampleDcbAggregate)] },
        new DomainTypeCatalogue { DcbAggregateIds = [typeof(SampleDcbAggregateId)] },
        new DomainTypeCatalogue { DcbProjections = [typeof(SampleDcbProjection)] },
        new DomainTypeCatalogue { DcbProjectionIds = [typeof(SampleDcbProjectionId)] },
        new DomainTypeCatalogue { Events = [typeof(SampleHappenedEvent)], DcbEvents = [typeof(SampleHappenedEvent)] });
}
