using System.Windows;

namespace DIS.Dashboard;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Ensure WebView2 runtime is initialised before navigation
        await DashboardWebView.EnsureCoreWebView2Async();

        // TODO: Configure WebView2 settings (disable context menu, disable dev tools in release).
        // TODO: Wire reload-on-connection-lost behaviour for when DIS.Host restarts.
    }
}
