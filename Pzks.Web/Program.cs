using System.Diagnostics;
using Pzks.Core.Parallelization;
using Pzks.Core.Samples;
using Pzks.Web;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/samples", () => MemoExpressions.Lab2.Select(SampleResponse.From));

app.MapPost("/api/parallel", (ParallelRequest request) =>
    ParallelResponse.From(ParallelTreeBuilder.Build(request.Expression ?? "")));

app.Lifetime.ApplicationStarted.Register(() => OpenBrowser(app.Urls.FirstOrDefault()));
app.Run();

static void OpenBrowser(string? url)
{
    if (url is null)
        return;
    Console.WriteLine($"Сторінка ЛР2: {url}");
    if (Environment.GetEnvironmentVariable("PZKS_NO_BROWSER") == "1")
        return;
    try
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
    catch (System.ComponentModel.Win32Exception)
    {
        Console.WriteLine("Не вдалося відкрити браузер автоматично — відкрийте адресу вручну.");
    }
}
