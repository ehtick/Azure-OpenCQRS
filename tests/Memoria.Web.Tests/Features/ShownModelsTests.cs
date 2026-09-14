using FluentAssertions;
using Memoria.Web.Components.Shared;
using Memoria.Web.Data;
using Memoria.Web.Extensibility;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// Which consistency models the site is laid out for: both side by side, or one alone when the
/// other has nothing registered under it.
/// </summary>
public class ShownModelsTests
{
    private static readonly StoreCapabilities Relational = new(HasDcb: true, CanUpdate: true);

    private static readonly StoreCapabilities Cosmos = new(HasDcb: false, CanUpdate: true);

    private static readonly DomainTypeCatalogue Nothing = DomainTypeCatalogue.Empty;

    private static readonly DomainTypeCatalogue StreamedOnly = new()
    {
        StreamedStreamIds = [typeof(SampleStreamId)]
    };

    private static readonly DomainTypeCatalogue DcbOnly = new()
    {
        DcbProjectionIds = [typeof(SampleDcbProjectionId)]
    };

    private static readonly DomainTypeCatalogue BothModels = new()
    {
        StreamedAggregates = [typeof(SampleAggregate)],
        DcbAggregates = [typeof(SampleDcbAggregate)]
    };

    [Fact]
    public void Shows_both_when_both_are_registered()
    {
        ShownModels.Of(BothModels, Relational).Should().Be(new ShownModels(Streamed: true, Dcb: true));
    }

    /// <summary>
    /// Nothing registered is nothing to narrow to: the site stays as it is until an upload says
    /// which model the domain uses.
    /// </summary>
    [Fact]
    public void Shows_both_when_nothing_is_registered_yet()
    {
        ShownModels.Of(Nothing, Relational).Should().Be(new ShownModels(Streamed: true, Dcb: true));
    }

    [Fact]
    public void Shows_the_streamed_model_alone_when_only_it_is_registered()
    {
        ShownModels.Of(StreamedOnly, Relational).Should().Be(new ShownModels(Streamed: true, Dcb: false));
    }

    [Fact]
    public void Shows_the_dcb_model_alone_when_only_it_is_registered()
    {
        ShownModels.Of(DcbOnly, Relational).Should().Be(new ShownModels(Streamed: false, Dcb: true));
    }

    /// <summary>
    /// A store with no boundary in it never shows the DCB model, whatever was uploaded: there is
    /// nothing those pages could read. The streamed model is what is left, and it stands alone.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryCatalogue))]
    public void Shows_the_streamed_model_alone_over_a_store_without_a_boundary(DomainTypeCatalogue catalogue)
    {
        ShownModels.Of(catalogue, Cosmos).Should().Be(new ShownModels(Streamed: true, Dcb: false));
    }

    [Fact]
    public void Says_when_both_are_shown()
    {
        new ShownModels(Streamed: true, Dcb: true).Both.Should().BeTrue();
        new ShownModels(Streamed: true, Dcb: false).Both.Should().BeFalse();
        new ShownModels(Streamed: false, Dcb: true).Both.Should().BeFalse();
    }

    public static TheoryData<DomainTypeCatalogue> EveryCatalogue => new(Nothing, StreamedOnly, DcbOnly, BothModels);
}
