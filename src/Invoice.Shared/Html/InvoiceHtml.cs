using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace InvoicePoc.Html;

/// <summary>
/// Renders <see cref="InvoiceTemplate"/> to a self-contained HTML string using .NET's HtmlRenderer
/// (available since .NET 8), so the template is a normal, strongly typed Razor component.
/// </summary>
public static class InvoiceHtml
{
    private static readonly Lazy<string> Css = new(BuildCss);

    public static async Task<string> RenderAsync(Invoice invoice)
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(InvoiceTemplate.Invoice)] = invoice,
                [nameof(InvoiceTemplate.Css)] = Css.Value.Replace("{{LEGAL}}", CssString(InvoiceText.LegalFooter(invoice))),
            });
            var output = await renderer.RenderComponentAsync<InvoiceTemplate>(parameters);
            return output.ToHtmlString();
        });

        return "<!DOCTYPE html>\n" + html;
    }

    /// <summary>Inline the fonts as data URIs so any engine can render the HTML without file or network access.</summary>
    private static string BuildCss()
    {
        var css = System.Text.Encoding.UTF8.GetString(Brand.LoadResource("Html.invoice.css"));
        return css
            .Replace("url(\"Lato-Regular.ttf\")", $"url(\"data:font/ttf;base64,{Convert.ToBase64String(Brand.LatoRegular)}\")")
            .Replace("url(\"Lato-Bold.ttf\")", $"url(\"data:font/ttf;base64,{Convert.ToBase64String(Brand.LatoBold)}\")");
    }

    /// <summary>Renders the layout stress test (see Stress/StressData.cs).</summary>
    public static async Task<string> RenderStressAsync()
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var parameters = ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(StressTemplate.Css)] = StressCss.Value });
            return (await renderer.RenderComponentAsync<StressTemplate>(parameters)).ToHtmlString();
        });
        return "<!DOCTYPE html>\n" + html;
    }

    private static readonly Lazy<string> StressCss = new(() =>
    {
        string Font(byte[] data) => $"url(\"data:font/ttf;base64,{Convert.ToBase64String(data)}\")";
        // Small (22 px) copy of the SVG logo for the running header's margin box.
        var headerLogo = Stress.StressAssets.LogoSvg.Replace("width=\"120\" height=\"120\"", "width=\"22\" height=\"22\"");
        return System.Text.Encoding.UTF8.GetString(Brand.LoadResource("Html.stress.css"))
            .Replace("url(\"Lato-Regular.ttf\")", Font(Brand.LatoRegular))
            .Replace("url(\"Lato-Bold.ttf\")", Font(Brand.LatoBold))
            .Replace("url(\"DMSerifDisplay-Regular.ttf\")", Font(Stress.StressAssets.SerifDisplay))
            .Replace("url(\"IBMPlexMono-Regular.ttf\")", Font(Stress.StressAssets.Mono))
            .Replace("url(\"Amiri-Regular.ttf\")", Font(Stress.StressAssets.Arabic))
            .Replace("url(\"DejaVuSans.ttf\")", Font(Stress.StressAssets.Fallback))
            .Replace("{{HEADER_LOGO}}", "data:image/svg+xml;base64," + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(headerLogo)))
            .Replace("{{HEADER_TEXT}}", CssString(Stress.StressData.HeaderText));
    });

    private static string CssString(string text) => text.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
