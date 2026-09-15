namespace InvoicePoc;

/// <summary>Helpers for the "append an existing T&amp;Cs PDF" tests.</summary>
public static class MergeSupport
{
    /// <summary>The third-party terms PDF: 3 pages, US Letter, 8 bookmarks, 1 hyperlink.</summary>
    public static string TermsPath
    {
        get
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "assets", "terms-and-conditions.pdf");
                if (File.Exists(candidate)) return candidate;
            }
            throw new FileNotFoundException("assets/terms-and-conditions.pdf not found. Generate it with: dotnet run --project src/Poc.IText -c Release -- make-terms");
        }
    }

    public static string TempInvoice() => Path.Combine(Path.GetTempPath(), $"invoice-{Guid.NewGuid():N}.pdf");
}
