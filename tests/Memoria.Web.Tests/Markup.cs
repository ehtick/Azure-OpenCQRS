using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Memoria.Web.Tests;

/// <summary>The parts of a rendered page a test asks about.</summary>
internal static class Markup
{
    /// <summary>
    /// The page as written, without the attribute scoped CSS stamps on every element of a
    /// component that has a stylesheet of its own: a test says what the page says, not which
    /// component said it.
    /// </summary>
    public static string Plain(string page) => Regex.Replace(page, " b-[a-z0-9]{10}(?=[ >/])", string.Empty);

    /// <summary>The page's header alone, so a word in the body does not stand in for one up there.</summary>
    public static string Header(string page)
    {
        page = Plain(page);
        var start = page.IndexOf("<header", StringComparison.Ordinal);
        var end = page.IndexOf("</header>", StringComparison.Ordinal);

        return start >= 0 && end > start ? page[start..end] : string.Empty;
    }

    /// <summary>The page's breadcrumb alone, so a link in the body does not stand in for a crumb.</summary>
    public static string Breadcrumb(string page)
    {
        page = Plain(page);
        var start = page.IndexOf("<nav class=\"breadcrumb\"", StringComparison.Ordinal);
        var end = start >= 0 ? page.IndexOf("</nav>", start, StringComparison.Ordinal) : -1;

        return start >= 0 && end > start ? page[start..end] : string.Empty;
    }

    /// <summary>
    /// The labels along the top of the header's main menu, in order: each section heading and each
    /// plain link, without what is folded under a heading.
    /// </summary>
    public static string[] MenuBar(string page)
    {
        // The first nav is the bar; the operator's own menu is a second one after it.
        var header = Header(page);
        var start = Regex.Match(header, "<nav(\\s[^>]*)?>").Index;
        var end = header.IndexOf("</nav>", StringComparison.Ordinal);
        var nav = end > start ? header[start..end] : string.Empty;

        // A heading is a <summary>; a plain link is an <a> that is not folded under one.
        var labels = new List<string>();
        var folded = false;

        foreach (Match match in Regex.Matches(
                     nav, "<summary[^>]*>(?<summary>[^<]*)</summary>|<div class=\"submenu\"|</details>|<a [^>]*>(?<link>[^<]*)</a>"))
        {
            if (match.Groups["summary"].Success)
            {
                labels.Add(match.Groups["summary"].Value.Trim());
            }
            else if (match.Value.StartsWith("<div", StringComparison.Ordinal))
            {
                folded = true;
            }
            else if (match.Value == "</details>")
            {
                folded = false;
            }
            else if (!folded)
            {
                labels.Add(match.Groups["link"].Value.Trim());
            }
        }

        return [.. labels];
    }
}
