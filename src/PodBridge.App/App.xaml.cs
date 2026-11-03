using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace PodBridge.App;

public partial class App : Application
{
    private const string MutexName = @"Local\PodBridge.SingleInstance";
    private const string ShowEventName = @"Local\PodBridge.ShowWindow";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showWait;

    public App()
    {
        InitializeComponent();

        // PodBridge lives in the tray: hiding or closing windows must never end the process.
        // Only "Exit" in the tray menu (Application.Exit) shuts it down.
        DispatcherShutdownMode = DispatcherShutdownMode.OnExplicitShutdown;

        UnhandledException += (_, e) =>
        {
            // Never crash silently: write a small local log and keep running.
            Log.Error("Unhandled exception", e.Exception);
            e.Handled = true;
        };
    }

    internal static AppHost Host { get; private set; } = null!;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _mutex = new Mutex(true, MutexName, out bool isFirstInstance);
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);

        if (!isFirstInstance)
        {
            // PodBridge is already running (probably in the tray). Ask it to show its window.
            _showEvent.Set();
            Environment.Exit(0);
            return;
        }

        bool background = Environment.GetCommandLineArgs().Contains("--background", StringComparer.OrdinalIgnoreCase);
        var dispatcher = DispatcherQueue.GetForCurrentThread();

        Host = new AppHost(dispatcher);
        Host.Start(background);

        _showWait = ThreadPool.RegisterWaitForSingleObject(
            _showEvent,
            (_, _) => dispatcher.TryEnqueue(() => Host.ShowMainWindow()),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }
}
