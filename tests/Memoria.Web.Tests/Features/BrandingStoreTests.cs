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
    public void Trims_the_name()
    {
        var store = Store();

        store.Save("  Contoso  ");

        store.Current.Name.Should().Be("Contoso");
    }

    /// <summary>
    /// A blank name is a name left out, so the logo stands alone in the header — Memoria's mark
    /// or their own — and it is remembered that way.
    /// </summary>
    [Theory]
    [InlineData(BrandChoice.Memoria)]
    [InlineData(BrandChoice.Own)]
    public void Leaves_the_name_out_when_it_is_blank_so_the_logo_stands_alone(BrandChoice choice)
    {
        var store = Store();
        store.Save("Contoso", new MemoryStream(Png()));

        store.Save("   ", choice);

        using (new AssertionScope())
        {
            store.Current.Name.Should().BeEmpty();
            store.Current.HasName.Should().BeFalse();
            store.Current.LogoChoice.Should().Be(choice);
            Store().Current.Name.Should().BeEmpty();
        }
    }

    /// <summary>
    /// Neither a name nor a logo is a choice too: the header then draws no brand at all and starts
    /// with its links.
    /// </summary>
    [Fact]
    public void Draws_no_brand_at_all_with_neither_name_nor_logo_and_remembers_it()
    {
        var store = Store();
        store.Save("Contoso", new MemoryStream(Png()));

        store.Save(BrandChoice.None, null, BrandChoice.None);

        using (new AssertionScope())
        {
            store.Current.IsShown.Should().BeFalse();
            store.Current.Name.Should().BeEmpty();
            Store().Current.IsShown.Should().BeFalse();
        }
    }

    [Theory]
    [InlineData(BrandChoice.Memoria, BrandChoice.None)]
    [InlineData(BrandChoice.None, BrandChoice.Memoria)]
    [InlineData(BrandChoice.Own, BrandChoice.None)]
    public void Draws_a_brand_while_either_half_of_it_is_drawn(BrandChoice name, BrandChoice logo)
    {
        var store = Store();

        store.Save(name, "Contoso", logo);

        store.Current.IsShown.Should().BeTrue();
    }

    /// <summary>
    /// The name follows the logo's three choices. Memoria's is drawn as Memoria, and the name
    /// they typed is kept beside it, to be chosen again without typing it again.
    /// </summary>
    [Fact]
    public void Draws_Memoria_s_name_when_chosen_and_keeps_their_own_for_later()
    {
        var store = Store();
        store.Save("Contoso");

        store.Save(BrandChoice.Memoria, "Contoso", BrandChoice.Memoria);

        using (new AssertionScope())
        {
            store.Current.NameChoice.Should().Be(BrandChoice.Memoria);
            store.Current.Name.Should().Be("Memoria");
            store.Current.OwnName.Should().Be("Contoso");
            Store().Current.OwnName.Should().Be("Contoso");
        }
    }

    [Fact]
    public void Refuses_their_own_name_left_blank_and_keeps_what_was_there()
    {
        var store = Store();
        store.Save("Contoso");

        var saving = () => store.Save(BrandChoice.Own, "  ", BrandChoice.Memoria);

        using (new AssertionScope())
        {
            saving.Should().Throw<InvalidDataException>().WithMessage("Type the name*");
            store.Current.Name.Should().Be("Contoso");
        }
    }

    /// <summary>
    /// A file written before the name was a choice says only the name: blank there is no name, so
    /// with no logo either it draws no brand.
    /// </summary>
    [Fact]
    public void Reads_a_blank_name_from_an_earlier_file_as_no_name()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "branding.json"), """{ "name": "", "version": 2, "noLogo": true }""");

        var current = Store().Current;

        using (new AssertionScope())
        {
            current.NameChoice.Should().Be(BrandChoice.None);
            current.IsShown.Should().BeFalse();
        }
    }

    [Fact]
    public void Reads_their_own_name_chosen_with_none_typed_as_Memoria_s()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "branding.json"), """{ "name": "", "nameChoice": "own", "version": 2 }""");

        Store().Current.Name.Should().Be("Memoria");
    }

    [Fact]
    public void Reads_a_file_with_no_name_setting_as_Memoria_s_name()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "branding.json"), """{ "version": 2 }""");

        Store().Current.Name.Should().Be("Memoria");
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
        Store().Current.LogoChoice.Should().Be(BrandChoice.Memoria);
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

        store.Save("Contoso", BrandChoice.None);

        using (new AssertionScope())
        {
            store.Current.LogoChoice.Should().Be(BrandChoice.None);
            store.Current.HasLogo.Should().BeFalse();
            store.LogoPath.Should().BeNull();
            Files().Should().NotContain(name => name.StartsWith("logo"));
            Store().Current.LogoChoice.Should().Be(BrandChoice.None);
            Store().Current.Name.Should().Be("Contoso");
        }
    }

    [Fact]
    public void Keeps_the_logo_removed_when_only_the_name_changes()
    {
        var store = Store();
        store.Save("Contoso", BrandChoice.None);

        store.Save("Fabrikam");

        store.Current.LogoChoice.Should().Be(BrandChoice.None);
    }

    [Fact]
    public void Goes_back_to_Memoria_s_mark_and_keeps_the_name()
    {
        var store = Store();
        store.Save("Contoso", new MemoryStream(Png()));

        store.Save("Contoso", BrandChoice.Memoria);

        using (new AssertionScope())
        {
            store.Current.LogoChoice.Should().Be(BrandChoice.Memoria);
            store.Current.Name.Should().Be("Contoso");
            store.LogoPath.Should().BeNull();
            Files().Should().NotContain(name => name.StartsWith("logo"));
        }
    }

    /// <summary>A file sent is a logo meant, whichever choice came with it.</summary>
    [Theory]
    [InlineData(BrandChoice.Memoria)]
    [InlineData(BrandChoice.None)]
    [InlineData(BrandChoice.Own)]
    public void Takes_an_uploaded_logo_as_the_owner_s_whatever_was_chosen(BrandChoice chosen)
    {
        var store = Store();
        store.Save("Contoso", BrandChoice.None);

        store.Save("Contoso", chosen, new MemoryStream(Png()));

        using (new AssertionScope())
        {
            store.Current.LogoChoice.Should().Be(BrandChoice.Own);
            File.ReadAllBytes(store.LogoPath!).Should().Equal(Png());
        }
    }

    [Fact]
    public void Keeps_the_uploaded_logo_when_its_own_is_chosen_again_without_a_file()
    {
        var store = Store();
        store.Save("Contoso", new MemoryStream(Png()));

        store.Save("Fabrikam", BrandChoice.Own);

        using (new AssertionScope())
        {
            store.Current.LogoChoice.Should().Be(BrandChoice.Own);
            File.ReadAllBytes(store.LogoPath!).Should().Equal(Png());
        }
    }

    [Fact]
    public void Refuses_its_own_logo_with_none_uploaded_and_keeps_what_was_there()
    {
        var store = Store();
        store.Save("Contoso", BrandChoice.None);

        var saving = () => store.Save("Fabrikam", BrandChoice.Own);

        using (new AssertionScope())
        {
            saving.Should().Throw<InvalidDataException>().WithMessage("Choose an image*");
            store.Current.Name.Should().Be("Contoso");
            store.Current.LogoChoice.Should().Be(BrandChoice.None);
        }
    }

    [Fact]
    public void Is_not_the_default_with_the_logo_removed()
    {
        var store = Store();

        store.Save("Memoria", BrandChoice.None);

        store.Current.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void Restores_Memoria_s_mark_after_the_logo_was_removed()
    {
        var store = Store();
        store.Save("Contoso", BrandChoice.None);

        store.Reset();

        store.Current.LogoChoice.Should().Be(BrandChoice.Memoria);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
