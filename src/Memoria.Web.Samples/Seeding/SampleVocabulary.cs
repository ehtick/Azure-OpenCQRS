namespace Memoria.Web.Samples.Seeding;

/// <summary>
/// The words the sample data is made of, and the small random helpers that pick between them.
/// </summary>
/// <remarks>
/// Identifiers are readable and short on purpose. Someone reading a run's report has to type them
/// back into the web tool by hand, and a GUID is a poor thing to copy off a terminal. What keeps
/// them apart is counted rather than drawn: see <see cref="Id"/>.
/// </remarks>
public static class SampleVocabulary
{
    private static readonly string[] Products =
    [
        "Espresso Machine", "Walnut Desk", "Linen Apron", "Cast Iron Pan", "Reading Lamp",
        "Wool Blanket", "Chef's Knife", "Canvas Holdall", "Ceramic Mug", "Bamboo Chopping Board",
        "Leather Notebook", "Enamel Kettle"
    ];

    private static readonly string[] Carriers = ["Royal Mail", "DPD", "Evri", "DHL"];

    private static readonly string[] Warehouses = ["LDN-1", "MAN-2", "GLA-3"];

    private static readonly string[] CancellationReasons =
    [
        "changed their mind", "found it cheaper elsewhere", "ordered the wrong size",
        "no longer needed"
    ];

    private static readonly string[] DiscontinuationReasons =
    [
        "supplier stopped making it", "replaced by the new model", "poor margin"
    ];

    private static readonly string[] Keywords =
    [
        "kitchen", "gift", "handmade", "compact", "best seller", "outdoor", "hard wearing",
        "clearance", "premium", "everyday"
    ];

    private static readonly string[] Finishes =
    [
        "Oak", "Charcoal", "Brass", "Sage", "Ivory", "Slate", "Copper"
    ];

    private static readonly string[] AdjustmentReasons =
    [
        "stock count", "damaged in the warehouse", "found behind the racking"
    ];

    private static readonly string[] Buyers =
    [
        "a.patel", "j.morrison", "s.okafor", "m.reid"
    ];

    private static readonly string[] PurchaseOrderCancellationReasons =
    [
        "supplier could not deliver in time", "raised against the wrong supplier",
        "the shelves filled up without it"
    ];

    private static readonly string[] ReviewTitles =
    [
        "Does the job", "Better than expected", "Not what the picture showed",
        "Sturdy and well made", "Would buy again", "Arrived scratched"
    ];

    private static readonly string[] ReviewBodies =
    [
        "Exactly as described and quick to arrive.",
        "Solid enough, though the finish is duller than the photos.",
        "Three weeks in and it is already showing wear.",
        "A gift for my sister, who has not stopped mentioning it since.",
        "Replaced the one I had for a decade. This one feels like it will last as long.",
        "Smaller than I expected, which turned out to be exactly right for the kitchen."
    ];

    private static readonly string[] ReviewRemovalReasons =
    [
        "the author asked for it to be taken down", "it broke the house rules",
        "it reviewed the courier rather than the product"
    ];

    public static string ProductName(Random random) => Pick(random, Products);

    public static string Carrier(Random random) => Pick(random, Carriers);

    public static string Warehouse(Random random) => Pick(random, Warehouses);

    public static string CancellationReason(Random random) => Pick(random, CancellationReasons);

    public static string DiscontinuationReason(Random random) => Pick(random, DiscontinuationReasons);

    public static string AdjustmentReason(Random random) => Pick(random, AdjustmentReasons);

    public static string Buyer(Random random) => Pick(random, Buyers);

    public static string PurchaseOrderCancellationReason(Random random) =>
        Pick(random, PurchaseOrderCancellationReasons);

    public static string ReviewTitle(Random random) => Pick(random, ReviewTitles);

    public static string ReviewBody(Random random) => Pick(random, ReviewBodies);

    public static string ReviewRemovalReason(Random random) => Pick(random, ReviewRemovalReasons);

    /// <summary>
    /// An identifier of the given kind, short enough to retype.
    /// </summary>
    /// <remarks>
    /// Counted rather than drawn. An identifier is the key a model is stored under, so the second
    /// one issued twice is not a coincidence the store shrugs off — it is a row rejected by a
    /// primary key half way through a run. Four hexadecimal digits, which is what this used to
    /// draw, are sixty thousand identifiers, and a run of five hundred customers has better than
    /// an even chance of drawing one of them twice; a large run found it every time.
    /// </remarks>
    public static string Id(string prefix) => $"{prefix}-{Token()}";

    /// <summary>
    /// A stock keeping unit, made from the product's name so the two are recognisably the same
    /// thing in a listing.
    /// </summary>
    public static string Sku(string productName)
    {
        var letters = new string(productName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]))
            .ToArray());

        return $"{letters}-{Token()}";
    }

    /// <summary>
    /// A few words a catalogue search might find the product by, without repeating one.
    /// </summary>
    public static IReadOnlyList<string> ProductKeywords(Random random, int count) =>
        [..Keywords.OrderBy(_ => random.Next()).Take(Math.Min(count, Keywords.Length))];

    /// <summary>
    /// The finishes a product is sold in, without repeating one.
    /// </summary>
    public static IReadOnlyList<string> ProductFinishes(Random random, int count) =>
        [..Finishes.OrderBy(_ => random.Next()).Take(Math.Min(count, Finishes.Length))];

    /// <summary>
    /// A price with two decimal places, which is what a price has.
    /// </summary>
    public static decimal Price(Random random, int lowest, int highest) =>
        Math.Round(random.Next(lowest * 100, highest * 100) / 100m, 2);

    /// <summary>
    /// A measurement with one decimal place, which is as precise as a tape measure or a set of
    /// warehouse scales gets.
    /// </summary>
    public static decimal Measurement(Random random, int lowest, int highest) =>
        Math.Round(random.Next(lowest * 10, highest * 10) / 10m, 1);

    /// <summary>
    /// A moment in the last few weeks, so a listing sorted by date is not all one instant.
    /// </summary>
    public static DateTimeOffset RecentMoment(Random random, TimeProvider time) =>
        time.GetUtcNow()
            .AddDays(-random.Next(1, 30))
            .AddMinutes(-random.Next(0, 1440));

    /// <summary>
    /// True with the given chance, written as a percentage so the call sites read as odds.
    /// </summary>
    public static bool Chance(Random random, int percent) => random.Next(100) < percent;

    private static string Pick(Random random, string[] from) => from[random.Next(from.Length)];

    /// <summary>The digits identifiers are counted in: as many as a keyboard offers per stroke.</summary>
    private const string Digits = "0123456789abcdefghijklmnopqrstuvwxyz";

    /// <summary>
    /// How many seconds a run's mark covers before it comes round again, which is a little under
    /// three weeks. Long enough that a store is unlikely to still hold a run that old, and short
    /// enough that the mark is four characters rather than seven.
    /// </summary>
    private const long Cycle = 36L * 36 * 36 * 36;

    /// <summary>What this run's identifiers start with, so no earlier run's can be one of them.</summary>
    private static readonly string ThisRun = Run(DateTimeOffset.UtcNow, new Random().Next(36));

    /// <summary>How many identifiers this run has issued.</summary>
    private static int _issued;

    /// <summary>
    /// What marks one run's identifiers apart from another's: the second it started in, and a
    /// character drawn for the two runs that start in the same one.
    /// </summary>
    /// <remarks>
    /// The moment does the work, because the runs that must not collide are runs against the same
    /// store and those happen minutes apart, not microseconds. The drawn character is for the pair
    /// that really does start together — two terminals, or a run restarted the instant it was
    /// stopped — where a moment alone would hand both the same identifiers.
    /// </remarks>
    internal static string Run(DateTimeOffset moment, int drawn) =>
        Counted(moment.ToUnixTimeSeconds() % Cycle).PadLeft(4, '0') + Digits[drawn % Digits.Length];

    /// <summary>
    /// The next identifier nobody has had, which is this run's mark and a count after it.
    /// </summary>
    /// <remarks>
    /// Counted under a lock of its own, so that a run issuing identifiers from more than one place
    /// at once still issues each of them once.
    /// </remarks>
    private static string Token() => ThisRun + Counted(Interlocked.Increment(ref _issued));

    private static string Counted(long count)
    {
        var digits = new Stack<char>();

        do
        {
            digits.Push(Digits[(int)(count % Digits.Length)]);
            count /= Digits.Length;
        }
        while (count > 0);

        return new string(digits.ToArray());
    }
}
