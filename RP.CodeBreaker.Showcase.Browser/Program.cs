using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using RP.CodeBreaker.Showcase;

// The browser build of the showcase: the same API the ASP.NET server maps onto /api/, exported to
// JavaScript so a static host such as GitHub Pages can run every attack with no server at all.
// Main has nothing to do; the runtime stays loaded and the worker calls Call as the pages need it.
return;

[SupportedOSPlatform("browser")]
public static partial class ShowcaseExports
{
    /// <summary>
    /// Runs <paramref name="operation"/> (for example <c>break</c>) on a JSON request and returns the
    /// JSON response. A bad request throws, which JavaScript receives as an exception. Long searches
    /// report progress through the worker's <c>codeBreakerProgress</c> function as they go.
    /// </summary>
    [JSExport]
    public static string Call(string operation, string requestJson) =>
        ShowcaseApi.Call(operation, requestJson, Progress);

    /// <summary>Hands a progress report to the worker, which posts it to the page at once.</summary>
    [JSImport("globalThis.codeBreakerProgress")]
    internal static partial void Progress(string json);
}
