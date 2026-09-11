namespace InvoicePoc.Stress;

/// <summary>One statement line. Thumbnail is an embedded PNG name (or null).</summary>
public sealed record StatementRow(DateOnly Date, string Reference, string Description, string? Thumbnail, bool Paid, decimal Amount);

/// <summary>
/// Content for the layout stress test. Every library renders exactly this, so each difficult
/// feature is exercised identically:
///  - three custom font families, right-to-left Arabic, Welsh diacritics, glyph fallback (✓ isn't in Lato)
///  - running header skipped on page 1, footer text that differs on the last page, "Page X of Y"
///  - a 70-row table whose header repeats on every page and whose rows never split
///  - SVG logo (with gradient), JPEG scaled to width with a caption kept together, PNGs with alpha in table cells
///  - two-column flowing terms and a signature box that must not split
/// </summary>
public static class StressData
{
    public const string Title = "Statement of account";
    public const string StatementDate = "10 September 2026";
    public const string AccountRef = "ACC-00417-HF";           // rendered in IBM Plex Mono
    public const string CustomerName = "Harbour & Finch Ltd";
    public static readonly string[] BranchAddress = ["Canolfan Dŵr, Tŷ Gwydr", "Heol y Frenhines", "Caerdydd", "CF10 2BH"]; // ŵ ŷ
    public const string ArabicTradingName = "شركة الخليج للتجارة";  // "Gulf Trading Company": needs RTL + joined letterforms
    public const string ArabicLabel = "Registered trading name (Arabic)";
    public const string PhotoCaption = "Site visit: Cardiff office fit-out, 14 August 2026.";
    public const string TransactionsHeading = "Transactions";
    public const string PaidMark = "Paid ✓";                    // ✓ (U+2713) is not in Lato: tests fallback
    public const string HeaderText = "Statement of account for Harbour & Finch Ltd (continued)";
    public const string ContinuedText = "Continued on next page";
    public const string EndText = "End of statement";
    public const string TermsHeading = "Terms and conditions";
    public const string RemittanceHeading = "Remittance advice";

    public static readonly string[] Columns = ["Date", "Reference", "", "Description", "Status", "Amount"];

    public static IReadOnlyList<StatementRow> Rows()
    {
        string[] services =
        [
            "Support retainer", "Azure hosting and monitoring", "Backend development", "UX design sprint",
            "Accessibility audit and remediation plan for the customer portal, including screen-reader testing on NVDA and VoiceOver",
            "Printed training manuals", "Security review", "Data migration from the legacy invoicing system (phase {0}), with reconciliation reports",
        ];
        string[] thumbs = ["thumb-cloud.png", "thumb-box.png", "thumb-laptop.png"];
        var rows = new List<StatementRow>();
        var date = new DateOnly(2026, 3, 2);
        for (var i = 1; i <= 70; i++)
        {
            var isPayment = i % 6 == 0;
            var desc = isPayment ? "Payment received, thank you" : string.Format(services[i % services.Length], (i / 8) + 1);
            var amount = isPayment ? -Money.Round(1450m + i * 37.5m) : Money.Round(180m + (i * 97.35m % 2400m));
            rows.Add(new StatementRow(
                date,
                isPayment ? $"PAY-{8800 + i}" : $"INV-2026-{100 + i:0000}",
                desc,
                i % 7 == 0 ? thumbs[i / 7 % thumbs.Length] : null,
                Paid: i <= 44,
                amount));
            date = date.AddDays(i % 3 == 0 ? 4 : 2);
        }
        return rows;
    }

    public static decimal Balance => Rows().Where(r => !r.Paid).Sum(r => r.Amount);

    public static string ShortDate(DateOnly d) => d.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);

    public static readonly string[] Terms =
    [
        "1. Payment. Invoices are payable within 30 days of the invoice date unless a different period has been agreed in writing. Please quote the invoice number as your payment reference so that we can allocate funds promptly.",
        "2. Late payment. We reserve the right to claim interest and compensation under the Late Payment of Commercial Debts (Interest) Act 1998. Statutory interest is 8% a year above the Bank of England base rate, plus fixed compensation of £40, £70 or £100 depending on the size of the debt.",
        "3. Queries. If you believe any item on this statement is incorrect, please tell us within 14 days. Undisputed amounts remain payable while a query is investigated, and we will issue a credit note if an adjustment is due.",
        "4. Credit limits. We may review your credit limit at any time. Where the balance exceeds the agreed limit, we may pause work until the account is brought back within terms, after giving you reasonable notice.",
        "5. Data protection. We process personal data in line with UK GDPR and the Data Protection Act 2018. Contact details held for accounts purposes are used only to administer your account and are retained for six years after it closes.",
        "6. Governing law. These terms are governed by the law of England and Wales, and the courts of England and Wales have exclusive jurisdiction over any dispute arising from them.",
    ];

    public static readonly (string Label, string Value)[] RemittanceFields =
    [
        ("Account reference", AccountRef),
        ("Amount enclosed", "£ ____________"),
        ("Signed", "______________________________"),
        ("Date", "____ / ____ / ________"),
    ];
}

/// <summary>Embedded images and extra fonts for the stress test.</summary>
public static class StressAssets
{
    public static string LogoSvg => System.Text.Encoding.UTF8.GetString(Load("logo.svg"));
    public static byte[] LogoPng => Load("logo.png");
    public static byte[] Photo => Load("site-photo.jpg");            // 1600 x 900 JPEG
    public static byte[] Thumbnail(string name) => Load(name);        // 96 x 96 PNG with alpha

    public static byte[] SerifDisplay => Load("DMSerifDisplay-Regular.ttf"); // headings
    public static byte[] Mono => Load("IBMPlexMono-Regular.ttf");            // references
    public static byte[] Arabic => Load("Amiri-Regular.ttf");                // Arabic
    public static byte[] Fallback => Load("DejaVuSans.ttf");                 // symbols missing from Lato (✓)

    public const string SerifFamily = "DM Serif Display", MonoFamily = "IBM Plex Mono", ArabicFamily = "Amiri", FallbackFamily = "DejaVu Sans";

    private static readonly Dictionary<string, byte[]> Cache = new();
    private static byte[] Load(string name)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(name, out var bytes)) Cache[name] = bytes = Brand.LoadResource("Stress." + name);
            return bytes;
        }
    }
}
