using System.Globalization;

namespace InvoicePoc;

/// <summary>Design tokens shared by all POCs so the output is directly comparable.</summary>
public static class Brand
{
    public const string FontFamily = "Lato";
    public const string Wordmark = "Brightwater Digital";

    public const string Accent = "#0F5257";      // deep canal teal: wordmark, amount-due panel, section headings
    public const string AccentTint = "#E6F0EF";  // table header fill
    public const string Ink = "#21302F";         // body text
    public const string Muted = "#5B6B6D";       // labels and footer
    public const string Rule = "#C5D3D2";        // hairlines between rows
    public const string OnAccent = "#FFFFFF";

    // Sizes in points
    public const float BodySize = 9.5f;
    public const float SmallSize = 7.5f;
    public const float WordmarkSize = 17f;
    public const float TitleSize = 22f;
    public const float AmountDueSize = 20f;
    public const float GrandTotalSize = 11f;

    // Page geometry in millimetres (A4)
    public const float MarginTopMm = 14, MarginSideMm = 16, MarginBottomMm = 20;

    public static float Mm(float mm) => mm * 72f / 25.4f;

    public static byte[] LatoRegular => Regular.Value;
    public static byte[] LatoBold => Bold.Value;

    private static readonly Lazy<byte[]> Regular = new(() => LoadResource("Fonts.Lato-Regular.ttf"));
    private static readonly Lazy<byte[]> Bold = new(() => LoadResource("Fonts.Lato-Bold.ttf"));

    public static (byte R, byte G, byte B) Rgb(string hex) =>
        (Convert.ToByte(hex[1..3], 16), Convert.ToByte(hex[3..5], 16), Convert.ToByte(hex[5..7], 16));

    internal static byte[] LoadResource(string logicalName)
    {
        using var stream = typeof(Brand).Assembly.GetManifestResourceStream(logicalName)
            ?? throw new InvalidOperationException($"Embedded resource '{logicalName}' not found.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}

/// <summary>UK formatting without depending on ICU culture data (works in minimal containers too).</summary>
public static class Fmt
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Money(decimal value) => (value < 0 ? "-£" : "£") + Math.Abs(value).ToString("#,##0.00", Inv);
    public static string Date(DateOnly date) => date.ToString("d MMMM yyyy", Inv);
    public static string Rate(decimal rate) => (rate * 100m).ToString("0.##", Inv) + "%";
    public static string Qty(LineItem line) => $"{line.Quantity.ToString("0.##", Inv)} {line.Unit}";
}

/// <summary>Wording shared by every template, so each library prints identical text.</summary>
public static class InvoiceText
{
    public static IEnumerable<string> AddressLines(Address a)
    {
        yield return a.Line1;
        if (!string.IsNullOrWhiteSpace(a.Line2)) yield return a.Line2!;
        yield return a.Town;
        yield return a.Postcode;
    }

    public static string OneLine(Address a) => string.Join(", ", AddressLines(a));

    public static IReadOnlyList<(string Label, string Value)> Meta(Invoice inv) =>
    [
        ("Invoice number", inv.Number),
        ("Invoice date", Fmt.Date(inv.IssueDate)),
        ("Tax point", Fmt.Date(inv.TaxPoint)),
        ("Due date", Fmt.Date(inv.DueDate)),
        ("Purchase order", inv.PurchaseOrder ?? "–"),
    ];

    public static IReadOnlyList<(string Label, string Value)> Payment(Invoice inv) =>
    [
        ("Bank", inv.Bank.BankName),
        ("Account name", inv.Bank.AccountName),
        ("Sort code", inv.Bank.SortCode),
        ("Account number", inv.Bank.AccountNumber),
        ("IBAN", inv.Bank.Iban),
        ("BIC", inv.Bank.Bic),
        ("Reference", inv.Number),
    ];

    public static string VatLabel(VatBand band) => $"VAT at {Fmt.Rate(band.Rate)} on {Fmt.Money(band.Net)}";

    public static string SupplierVatLine(Invoice inv) => $"VAT registration {inv.Supplier.VatNumber}";

    public static string LegalFooter(Invoice inv) =>
        $"{inv.Supplier.Name} is registered in England and Wales, company number {inv.Supplier.CompanyNumber}. " +
        $"Registered office: {OneLine(inv.Supplier.Address)}.";

    public static string PageOf(int page, int total) => $"Page {page} of {total}";

    public static readonly string[] ColumnHeaders = ["Description", "Qty", "Unit price", "VAT", "Amount"];

    public static string[] Row(LineItem l) =>
        [l.Description, Fmt.Qty(l), Fmt.Money(l.UnitPrice), Fmt.Rate(l.VatRate), Fmt.Money(l.Net)];
}
