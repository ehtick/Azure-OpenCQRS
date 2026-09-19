using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Memoria.Web.Branding;
using Xunit;

namespace Memoria.Web.Tests.Features;

/// <summary>
/// The name and logo an Administrator puts in the header, kept in a JSON file beside the logo
/// rather than in any store, and held in memory once read: every page draws the header, and none
/// of them should reach the disk to do it.
/// </summary>
public class BrandingStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"memoria-web-{Guid.NewGuid():N}");

    private BrandingStore Store() => new(_root);

    /// <summary>The first bytes of a PNG, which is all the store looks at to tell what it was sent.</summary>
    internal static byte[] Png(int size = 64)
    {
        var bytes = new byte[size];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        return bytes;
    }

    internal static byte[] Jpeg() => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

    internal static byte[] WebP() => "RIFF\0\0\0\0WEBPVP8 "u8.ToArray();

    internal static byte[] Svg() => "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8.ToArray();

    private string[] Files() =>
        Directory.Exists(_root) ? Directory.GetFiles(_root).Select(Path.GetFileName).ToArray()! : [];

    [Fact]
    public void Is_Memoria_with_its_own_mark_until_anything_is_saved()
    {
        var current = Store().Current;

        using (new AssertionScope())
        {
            current.Name.Should().Be("Memoria");
            current.HasLogo.Should().BeFalse();
        }
    }

    [Fact]
    public void Answers_a_saved_name_at_once()
    {
        var store = Store();

        store.Save("Contoso Ops");

        store.Current.Name.Should().Be("Contoso Ops");
    }

    [Fact]
    public void Reads_what_was_saved_back_after_a_restart()
    {
        Store().Save("Contoso Ops", new MemoryStream(Png()));

        var current = Store().Current;

        using (new AssertionScope())
        {
            current.Name.Should().Be("Contoso Ops");
            current.HasLogo.Should().BeTrue();
            File.ReadAllBytes(Store().LogoPath!).Should().Equal(Png());
        }
    }

    [Fact]
    public void Trims_the_name_and_falls_back_to_Memoria_when_it_is_blank()
    {
        var store = Store();

        store.Save("  Contoso  ");
        var trimmed = store.Current.Name;
        store.Save("   ");

        using (new AssertionScope())
        {
            trimmed.Should().Be("Contoso");
            store.Current.Name.Should().Be("Memoria");
        }
    }

    [Fact]
    public void Refuses_a_name_longer_than_sixty_characters_and_keeps_the_old_one()
    {
        var store = Store();
        store.Save("Contoso");

        var saving = () => store.Save(new string('x', 61));

        using (new AssertionScope())
        {
            saving.Should().Throw<InvalidDataException>().WithMessage("*60*");
            store.Current.Name.Should().Be("Contoso");
        }
    }

    [Fact]
    public void Accepts_a_name_of_exactly_sixty_characters()
    {
        var store = Store();

        store.Save(new string('x', 60));

        store.Current.Name.Should().HaveLength(60);
    }

    [Fact]
    public void Keeps_the_logo_when_only_the_name_changes()
    {
        var store = Store();
        store.Save("Contoso", new MemoryStream(Png()));

        store.Save("Fabrikam");

        store.Current.HasLogo.Should().BeTrue();
    }

    [Theory]
    [InlineData("png")]
    [InlineData("jpeg")]
    [InlineData("webp")]
    public void Accepts_png_jpeg_and_webp_by_what_the_file_holds(string kind)
    {
        var bytes = kind switch { "png" => Png(), "jpeg" => Jpeg(), _ => WebP() };
        var store = Store();

        store.Save("Contoso", new MemoryStream(bytes));

        using (new AssertionScope())
        {
            store.Current.LogoContentType.Should().Be($"image/{kind}");
            File.ReadAllBytes(store.LogoPath!).Should().Equal(bytes);
        }
    }

    /// <summary>
    /// An SVG can carry script, and the logo is served from this application's own origin; so it
    /// is refused by what it holds rather than what it is called, like anything else that is not one
    /// of the three.
    /// </summary>
    [Fact]
    public void Refuses_an_svg_and_keeps_what_was_there()
    {
        var store = Store();
        store.Save("Contoso", new MemoryStream(Png()));

        var saving = () => store.Save("Fabrikam", new MemoryStream(Svg()));

        using (new AssertionScope())
        {
            saving.Should().Throw<InvalidDataException>().WithMessage("*PNG, JPEG or WebP*");
            store.Current.Name.Should().Be("Contoso");
            File.ReadAllBytes(store.LogoPath!).Should().Equal(Png());
        }
    }

    [Fact]
    public void Refuses_a_logo_over_512_KB_and_accepts_one_of_exactly_that()
    {
        var store = Store();

        var tooLarge = () => store.Save("Contoso", new MemoryStream(Png(512 * 1024 + 1)));

        using (new AssertionScope())
        {
            tooLarge.Should().Throw<InvalidDataException>().WithMessage("*512 KB*");
            store.Current.HasLogo.Should().BeFalse();
            store.Save("Contoso", new MemoryStream(Png(512 * 1024)));
            store.Current.HasLogo.Should().BeTrue();
        }
    }

    [Fact]
    public void Replaces_the_logo_file_rather_than_leaving_the_old_one_beside_it()
    {
        var store = Store();
        store.Save("Contoso", new MemoryStream(Png()));

        store.Save("Contoso", new MemoryStream(WebP()));

        Files().Should().BeEquivalentTo("branding.json", "logo.webp");
    }

    /// <summary>
    /// Each save moves the version on, so the address the header draws the logo from changes
    /// with it and a browser that cached the old one asks again.
    /// </summary>
    [Fact]
    public void Moves_the_version_on_with_every_save()
    {
        var store = Store();
        var before = store.Current.Version;

        store.Save("Contoso");
        var once = store.Current.Version;
        store.Save("Contoso");

        using (new AssertionScope())
        {
            once.Should().BeGreaterThan(before);
            store.Current.Version.Should().BeGreaterThan(once);
            Store().Current.Version.Should().Be(store.Current.Version);
        }
    }

    [Fact]
    public void Restores_the_default_and_removes_the_files()
    {
        var store = Store();
        store.Save("Contoso", new MemoryStream(Png()));
        var saved = store.Current.Version;

        store.Reset();

        using (new AssertionScope())
        {
            store.Current.Name.Should().Be("Memoria");
            store.Current.HasLogo.Should().BeFalse();
            store.Current.Version.Should().BeGreaterThan(saved);
            store.LogoPath.Should().BeNull();
            Files().Should().NotContain(name => name.StartsWith("logo"));
            Store().Current.Name.Should().Be("Memoria");
        }
    }

    /// <summary>
    /// A file broken by hand is no reason for the tool not to start; it is drawn as Memoria until
    /// an Administrator saves over it.
    /// </summary>
    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{ "name": 42 }""")]
    [InlineData("""{ "name": "Contoso", "logo": "../../secrets.png" }""")]
    public void Reads_a_broken_or_tampered_file_as_the_default(string json)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "branding.json"), json);

        var store = Store();

        using (new AssertionScope())
        {
            store.Current.HasLogo.Should().BeFalse();
            store.LogoPath.Should().BeNull();
        }
    }

    [Fact]
    public void Reads_a_file_whose_logo_has_gone_as_having_no_logo()
    {
        var store = Store();
        store.Save("Contoso", new MemoryStream(Png()));
        File.Delete(store.LogoPath!);

        var current = Store().Current;

        using (new AssertionScope())
        {
            current.Name.Should().Be("Contoso");
            current.HasLogo.Should().BeFalse();
        }
    }

    [Fact]
    public void Draws_Memoria_s_mark_until_told_otherwise()
    {
        Store().Current.Choice.Should().Be(LogoChoice.Memoria);
    }

    /// <summary>
    /// No logo at all is a choice of its own, not the absence of one: the header shows the name
    /// alone, rather than falling back to Memoria's mark beside someone else's name.
    /// </summary>
    [Fact]
    public void Removes_the_logo_so_the_name_stands_alone_and_remembers_it()
    {
        var store = Store();
        store.Save("Contoso", new MemoryStream(Png()));

        store.Save("Contoso", LogoChoice.None);

        using (new AssertionScope())
        {
            store.Current.Choice.Should().Be(LogoChoice.None);
            store.Current.HasLogo.Should().BeFalse();
            store.LogoPath.Should().BeNull();
            Files().Should().NotContain(name => name.StartsWith("logo"));
            Store().Current.Choice.Should().Be(LogoChoice.None);
            Store().Current.Name.Should().Be("Contoso");
        }
    }

    [Fact]
    public void Keeps_the_logo_removed_when_only_the_name_changes()
    {
        var store = Store();
        store.Save("Contoso", LogoChoice.None);

        store.Save("Fabrikam");

        store.Current.Choice.Should().Be(LogoChoice.None);
    }

    [Fact]
    public void Goes_back_to_Memoria_s_mark_and_keeps_the_name()
    {
        var store = Store();
        store.Save("Contoso", new MemoryStream(Png()));

        store.Save("Contoso", LogoChoice.Memoria);

        using (new AssertionScope())
        {
            store.Current.Choice.Should().Be(LogoChoice.Memoria);
            store.Current.Name.Should().Be("Contoso");
            store.LogoPath.Should().BeNull();
            Files().Should().NotContain(name => name.StartsWith("logo"));
        }
    }

    /// <summary>A file sent is a logo meant, whichever choice came with it.</summary>
    [Theory]
    [InlineData(LogoChoice.Memoria)]
    [InlineData(LogoChoice.None)]
    [InlineData(LogoChoice.Own)]
    public void Takes_an_uploaded_logo_as_the_owner_s_whatever_was_chosen(LogoChoice chosen)
    {
        var store = Store();
        store.Save("Contoso", LogoChoice.None);

        store.Save("Contoso", chosen, new MemoryStream(Png()));

        using (new AssertionScope())
        {
            store.Current.Choice.Should().Be(LogoChoice.Own);
            File.ReadAllBytes(store.LogoPath!).Should().Equal(Png());
        }
    }

    [Fact]
    public void Keeps_the_uploaded_logo_when_its_own_is_chosen_again_without_a_file()
    {
        var store = Store();
        store.Save("Contoso", new MemoryStream(Png()));

        store.Save("Fabrikam", LogoChoice.Own);

        using (new AssertionScope())
        {
            store.Current.Choice.Should().Be(LogoChoice.Own);
            File.ReadAllBytes(store.LogoPath!).Should().Equal(Png());
        }
    }

    [Fact]
    public void Refuses_its_own_logo_with_none_uploaded_and_keeps_what_was_there()
    {
        var store = Store();
        store.Save("Contoso", LogoChoice.None);

        var saving = () => store.Save("Fabrikam", LogoChoice.Own);

        using (new AssertionScope())
        {
            saving.Should().Throw<InvalidDataException>().WithMessage("Choose an image*");
            store.Current.Name.Should().Be("Contoso");
            store.Current.Choice.Should().Be(LogoChoice.None);
        }
    }

    [Fact]
    public void Is_not_the_default_with_the_logo_removed()
    {
        var store = Store();

        store.Save("Memoria", LogoChoice.None);

        store.Current.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void Restores_Memoria_s_mark_after_the_logo_was_removed()
    {
        var store = Store();
        store.Save("Contoso", LogoChoice.None);

        store.Reset();

        store.Current.Choice.Should().Be(LogoChoice.Memoria);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
