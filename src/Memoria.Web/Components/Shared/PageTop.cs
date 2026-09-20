namespace Memoria.Web.Components.Shared;

/// <summary>
/// The beginning of a page, and the name a link back to it is written with.
/// </summary>
/// <remarks>
/// <para>
/// A table of stored rows is the one thing on this site that can run several screens, and the foot
/// of it is where a reader ends up: the pager, the rows-per-page picker and the end of the data are
/// all down there. Both ways back from it — a page link, and the <see cref="BackToTop"/> link under
/// the card — land here rather than leaving the view at the foot of a page whose first row is off
/// the top of the screen.
/// </para>
/// <para>
/// The beginning of the page is the bar, not the table and not the title: the bar does not stick,
/// so landing anywhere below it leaves it and the breadcrumb off the top of the screen, which is
/// not where a page starts. The bar is marked in the layout, once for the whole site, and is
/// focusable so that landing there moves the focus as well as the view — a reader on the keyboard
/// then carries on from the top rather than from the foot they left.
/// </para>
/// <para>
/// Here rather than on either of the components that use it, because neither owns it: the name has
/// to be the same word in three places — the id in the layout, the fragment a page link ends with,
/// and the fragment the link under the card carries.
/// </para>
/// </remarks>
internal static class PageTop
{
    /// <summary>
    /// What the layout marks the bar with. Also the name a browser falls back to when nothing on
    /// the page carries it, which is the same place, so the link cannot land anywhere unexpected.
    /// </summary>
    public const string Anchor = "top";

    /// <summary>The same name as the end of an address, which is what a link back to it carries.</summary>
    public const string Landing = $"#{Anchor}";
}
