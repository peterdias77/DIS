using System.Windows;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace DIS.Dashboard;

/// <summary>
/// WPF Application entry point.
/// Starts an in-process ASP.NET Core / Blazor Server host alongside the WPF window.
/// The Blazor UI is rendered inside the WPF window via Microsoft.Web.WebView2.
/// The Dashboard is intentionally READ-ONLY — no write path into the execution layer.
/// </summary>
public partial class App : Application
{
    private WebApplication? _blazorApp;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _blazorApp = BuildBlazorApp();
        await _blazorApp.StartAsync();

        new MainWindow().Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_blazorApp is not null)
            await _blazorApp.StopAsync();

        base.OnExit(e);
    }

    private static WebApplication BuildBlazorApp()
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseUrls("http://localhost:5100");

        builder.Services.AddRazorComponents()
               .AddInteractiveServerComponents();

        // TODO: Register IStateReadBus, ILogReader, SignalR hubs for live state push.

        var app = builder.Build();

        app.UseStaticFiles();
        app.UseAntiforgery();

        // TODO: Uncomment once Razor components are added:
        // app.MapRazorComponents<Components.App>()
        //    .AddInteractiveServerRenderMode();

        return app;
    }
}
