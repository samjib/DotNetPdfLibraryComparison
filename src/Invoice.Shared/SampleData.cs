namespace InvoicePoc;

public static class SampleData
{
    /// <summary>
    /// A fictional UK VAT invoice. It has 21 lines so it spills onto a second page,
    /// which tests pagination, repeating table headers and "Page X of Y" in every library.
    /// It also includes a zero-rated line and a tax point that differs from the invoice date,
    /// both of which HMRC requires to be shown.
    /// </summary>
    public static Invoice Invoice() => new(
        Number: "INV-2026-0147",
        IssueDate: new DateOnly(2026, 9, 10),
        TaxPoint: new DateOnly(2026, 8, 31),
        DueDate: new DateOnly(2026, 10, 10),
        PurchaseOrder: "PO-88213",
        Supplier: new Party(
            Name: "Brightwater Digital Ltd",
            Address: new Address("Unit 7, Lockside Works", "Old Mill Street", "Manchester", "M4 6AB"),
            VatNumber: "GB 123 4567 89",
            CompanyNumber: "12345678",
            Email: "accounts@brightwater.example"),
        Customer: new Party(
            Name: "Harbour & Finch Ltd",
            Address: new Address("Floor 3, Ropewalk House", "Quay Street", "Bristol", "BS1 4DB"),
            Attention: "Accounts Payable"),
        Lines:
        [
            new("Discovery workshop, on site", 2, "days", 850.00m, 0.20m),
            new("Service design: journey mapping", 16, "hrs", 95.00m, 0.20m),
            new("UX design: invoice portal wireframes", 24, "hrs", 95.00m, 0.20m),
            new("UX design: high-fidelity prototype", 18, "hrs", 95.00m, 0.20m),
            new("Accessibility review against WCAG 2.2 AA", 8, "hrs", 105.00m, 0.20m),
            new("Backend development: invoicing API (.NET 10)", 40, "hrs", 110.00m, 0.20m),
            new("Backend development: VAT calculation service", 22, "hrs", 110.00m, 0.20m),
            new("PDF generation proof of concept", 12, "hrs", 110.00m, 0.20m),
            new("Frontend development: Blazor components", 36, "hrs", 105.00m, 0.20m),
            new("Integration: accounting system connector", 14, "hrs", 110.00m, 0.20m),
            new("Test automation with Playwright", 16, "hrs", 100.00m, 0.20m),
            new("Code review and pairing", 6, "hrs", 120.00m, 0.20m),
            new("DevOps: CI/CD pipeline set-up", 10, "hrs", 115.00m, 0.20m),
            new("Azure hosting: App Service and SQL (August)", 1, "month", 240.00m, 0.20m),
            new("Azure Blob Storage and backups (August)", 1, "month", 38.50m, 0.20m),
            new("Application Insights monitoring (August)", 1, "month", 27.20m, 0.20m),
            new("Security review: dependency audit", 5, "hrs", 120.00m, 0.20m),
            new("Project management", 12, "hrs", 90.00m, 0.20m),
            new("Printed training manuals (zero-rated)", 12, "copies", 14.50m, 0.00m),
            new("Staff training session, half day on site", 1, "session", 450.00m, 0.20m),
            new("Support retainer (September)", 1, "month", 400.00m, 0.20m),
        ],
        Bank: new BankDetails(
            BankName: "Example Bank plc",
            AccountName: "Brightwater Digital Ltd",
            SortCode: "12-34-56",
            AccountNumber: "12345678",
            Iban: "GB33 BUKB 2020 1555 5555 55",
            Bic: "BUKBGB22"),
        Notes: "Payment terms: 30 days from the invoice date. Please use the invoice number as your payment reference.");
}
