using Letterist.Diagnostics;
using Letterist.Persistence;
using Microsoft.UI.Xaml;

namespace Letterist;

public partial class App : Application
{
    private Window? _mainWindow;

    public static bool AutomationMode { get; private set; }
    public static int AutomationPort { get; private set; } = 9221;
    public static bool SkipAutosaveRecovery { get; private set; }
    private static bool _automationModeOverriddenByArgs;
    private static bool _automationPortOverriddenByArgs;

    public App()
    {
        ApplyPreferenceDefaults();
        this.InitializeComponent();
        StartupLogger.Log("App ctor");
        this.UnhandledException += App_UnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        ParseCommandLineArgs();
    }

    private static void ApplyPreferenceDefaults()
    {
        try
        {
            var preferences = PreferencesStorage.Load();
            UiLocalizationService.Initialize(
                preferences.General.IsLanguageExplicitlySet ? preferences.General.Language : null,
                useSystemLanguageIfNotSet: true);
            AutomationMode = preferences.Automation.EnabledByDefault;
            AutomationPort = preferences.Automation.Port;
            StartupLogger.Configure(
                preferences.Automation.EnableLogging,
                string.IsNullOrWhiteSpace(preferences.Automation.LogFilePath)
                    ? null
                    : preferences.Automation.LogFilePath);
        }
        catch
        {
        }
    }

    private static void ParseCommandLineArgs()
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--automation")
            {
                AutomationMode = true;
                _automationModeOverriddenByArgs = true;
                SkipAutosaveRecovery = true;
            }
            else if (args[i] == "--port" && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out int port))
                {
                    AutomationPort = port;
                    _automationPortOverriddenByArgs = true;
                }
                i++;
            }
            else if (args[i] == "--skip-recovery")
            {
                SkipAutosaveRecovery = true;
            }
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        StartupLogger.Log("OnLaunched");
        _mainWindow = new MainWindow();
        _mainWindow.Activate();
    }

    public static void UpdateAutomationDefaults(bool enabledByDefault, int port)
    {
        if (!_automationModeOverriddenByArgs)
        {
            AutomationMode = enabledByDefault;
        }

        if (!_automationPortOverriddenByArgs)
        {
            AutomationPort = Math.Clamp(port, 1, 65535);
        }
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        StartupLogger.Log("App UnhandledException", e.Exception);
    }

    private void CurrentDomain_UnhandledException(object? sender, System.UnhandledExceptionEventArgs e)
    {
        StartupLogger.Log("Domain UnhandledException", e.ExceptionObject as Exception);
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        StartupLogger.Log("TaskScheduler UnobservedTaskException", e.Exception);
        e.SetObserved();
    }
}
