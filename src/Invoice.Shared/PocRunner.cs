using System.Diagnostics;
using System.Globalization;

namespace InvoicePoc;

/// <summary>
/// Runs a generator once "cold" (first document, includes engine start-up) and then several
/// times "warm" (engine already running), writes the PDF to /output and records the numbers.
/// One-off installs (e.g. downloading Chromium) should happen before calling this.
/// </summary>
public static class PocRunner
{
    public static Task<int> RunAsync(string library, string fileName, Action<Invoice, string> generate, int warmRuns = 5) =>
        RunAsync(library, fileName, (inv, path) => { generate(inv, path); return Task.CompletedTask; }, warmRuns);

    public static async Task<int> RunAsync(string library, string fileName, Func<Invoice, string, Task> generate, int warmRuns = 5)
    {
        var invoice = SampleData.Invoice();
        var outputDir = OutputDirectory();
        Directory.CreateDirectory(outputDir);
        var path = Path.Combine(outputDir, fileName);

        var stopwatch = Stopwatch.StartNew();
        await generate(invoice, path);
        var first = stopwatch.Elapsed;

        var scratch = Path.Combine(Path.GetTempPath(), $"poc-warm-{Guid.NewGuid():N}.pdf");
        stopwatch.Restart();
        for (var i = 0; i < warmRuns; i++) await generate(invoice, scratch);
        var warm = warmRuns > 0 ? stopwatch.Elapsed / warmRuns : TimeSpan.Zero;
        if (File.Exists(scratch)) File.Delete(scratch);

        var size = new FileInfo(path).Length;
        Console.WriteLine(
            $"{library}: wrote {path} ({size / 1024.0:0.0} KB). " +
            $"First document {first.TotalMilliseconds:0} ms, then {warm.TotalMilliseconds:0} ms average over {warmRuns} warm runs.");
        RecordResult(outputDir, library, fileName, size, first, warm);
        return 0;
    }

    /// <summary>output/ next to the solution file (override with POC_OUTPUT).</summary>
    public static string OutputDirectory()
    {
        var overridden = Environment.GetEnvironmentVariable("POC_OUTPUT");
        if (!string.IsNullOrWhiteSpace(overridden)) return overridden;

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "InvoicePdfPoc.sln"))) return Path.Combine(dir.FullName, "output");
        }
        return Path.Combine(Directory.GetCurrentDirectory(), "output");
    }

    private static void RecordResult(string outputDir, string library, string file, long size, TimeSpan first, TimeSpan warm)
    {
        var csv = Path.Combine(outputDir, "results.csv");
        var rows = File.Exists(csv)
            ? File.ReadAllLines(csv).Skip(1).Where(r => !r.StartsWith(library + ",", StringComparison.Ordinal)).ToList()
            : [];
        rows.Add(string.Join(',', library, file, size, (int)first.TotalMilliseconds, (int)warm.TotalMilliseconds,
            DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture)));
        File.WriteAllLines(csv, ["library,file,bytes,first_ms,warm_ms,utc", .. rows.OrderBy(r => r, StringComparer.Ordinal)]);
    }
}
