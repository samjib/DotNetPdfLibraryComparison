namespace InvoicePoc;

// The same model feeds every POC, so differences in the PDFs come from the libraries, not the data.

public sealed record Address(string Line1, string? Line2, string Town, string Postcode, string Country = "United Kingdom");

public sealed record Party(
    string Name,
    Address Address,
    string? VatNumber = null,
    string? CompanyNumber = null,
    string? Email = null,
    string? Attention = null);

/// <param name="Unit">Display unit, e.g. "hrs", "days", "month", "copies".</param>
/// <param name="VatRate">0.20m for standard rate, 0m for zero-rated items.</param>
public sealed record LineItem(string Description, decimal Quantity, string Unit, decimal UnitPrice, decimal VatRate)
{
    public decimal Net => Money.Round(Quantity * UnitPrice);
}

/// <summary>Net and VAT totals for one VAT rate (HMRC requires the VAT total; showing it per rate is clearer).</summary>
public sealed record VatBand(decimal Rate, decimal Net, decimal Vat);

public sealed record BankDetails(string BankName, string AccountName, string SortCode, string AccountNumber, string Iban, string Bic);

public sealed record Invoice(
    string Number,
    DateOnly IssueDate,
    DateOnly TaxPoint,
    DateOnly DueDate,
    string? PurchaseOrder,
    Party Supplier,
    Party Customer,
    IReadOnlyList<LineItem> Lines,
    BankDetails Bank,
    string Notes)
{
    public decimal Subtotal => Lines.Sum(l => l.Net);

    /// <summary>VAT is calculated per rate on the summed net amounts (one of the rounding methods HMRC accepts).</summary>
    public IReadOnlyList<VatBand> VatBands => Lines
        .GroupBy(l => l.VatRate)
        .OrderByDescending(g => g.Key)
        .Select(g =>
        {
            var net = g.Sum(l => l.Net);
            return new VatBand(g.Key, net, Money.Round(net * g.Key));
        })
        .ToList();

    public decimal VatTotal => VatBands.Sum(b => b.Vat);
    public decimal Total => Subtotal + VatTotal;
}

public static class Money
{
    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
