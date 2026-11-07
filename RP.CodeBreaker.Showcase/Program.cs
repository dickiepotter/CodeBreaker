using RP.CodeBreaker.Showcase;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// ---- Every operation: the API is shared with the WebAssembly build, so this server and the static
// GitHub Pages site give exactly the same answer to the same request — this one just runs native .NET. ----
app.MapPost("/api/{operation}", async (string operation, HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    string body = await reader.ReadToEndAsync();
    try
    {
        return Results.Content(ShowcaseApi.Call(operation, body), "application/json");
    }
    catch (ArgumentException error)
    {
        return Results.Text(error.Message, statusCode: StatusCodes.Status400BadRequest);
    }
});

app.Run();
