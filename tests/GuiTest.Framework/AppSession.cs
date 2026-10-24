using System.Diagnostics;
using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.Core.Exceptions;
using FlaUI.UIA3;

namespace GuiTest.Framework;

/// <summary>How to start the application under test.</summary>
public sealed record AppLaunchOptions(string ExecutablePath)
{
    /// <summary>Environment variables for the application process only — the test process stays untouched.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();

    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromSeconds(60);
    public TimeSpan ShutdownTimeout { get; init; } = TimeSpan.FromSeconds(15);
}

/// <summary>A running instance of the application under test, driven through UI Automation (UIA3). Disposing
/// closes the main window and kills the process if it does not exit in time.</summary>
public sealed class AppSession : IDisposable
{
    private readonly AppLaunchOptions _options;
    private readonly Process _process;

    private AppSession(AppLaunchOptions options, Process process, UIA3Automation automation, Window mainWindow)
    {
        _options = options;
        _process = process;
        Automation = automation;
        MainWindow = mainWindow;
    }

    public UIA3Automation Automation { get; }
    public Window MainWindow { get; }
    public int ProcessId => _process.Id;
    public bool HasExited => _process.HasExited;

    public static AppSession Launch(AppLaunchOptions options)
    {
        if (!File.Exists(options.ExecutablePath))
            throw new FileNotFoundException(
                $"Application under test not found: {options.ExecutablePath} — build it before running the GUI tests.",
                options.ExecutablePath);

        // Eigene Ausgabe-Pipes statt geerbter: sonst hält eine hängengebliebene App die Konsole des Testlaufs offen
        var start = new ProcessStartInfo(options.ExecutablePath)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(options.ExecutablePath),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var (name, value) in options.Environment)
            start.Environment[name] = value;

        var process = Process.Start(start)
            ?? throw new InvalidOperationException($"Could not start {options.ExecutablePath}.");
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        int processId = process.Id;
        var automation = new UIA3Automation();
        try
        {
            var mainWindow = WaitForMainWindow(process, automation, options.StartupTimeout);
            return new AppSession(options, process, automation, mainWindow);
        }
        catch
        {
            automation.Dispose();
            process.Dispose();
            Kill(processId);
            throw;
        }
    }

    /// <summary>Saves a PNG of the main window, e.g. as evidence for a failed test.</summary>
    public void CaptureScreenshot(string path) => Capture.Element(MainWindow).ToFile(path);

    public void Dispose()
    {
        Close();
        Automation.Dispose();
        _process.Dispose();
    }

    private void Close()
    {
        if (_process.HasExited) return;
        try
        {
            MainWindow.Close();
        }
        catch (Exception e) when (e is COMException or ElementNotAvailableException)
        {
            // Window already gone or not responding — the kill below takes over.
        }

        if (!_process.WaitForExit(_options.ShutdownTimeout))
            Kill(_process.Id);
    }

    private static Window WaitForMainWindow(Process process, UIA3Automation automation, TimeSpan timeout)
    {
        // Attach per id, not with our Process object: FlaUI's GetMainWindow leaves a passed-in Process unusable
        // ("No process is associated with this object") while the window does not exist yet (verified #521)
        var application = FlaUI.Core.Application.Attach(process.Id);
        return Wait.UntilFound(
            () =>
            {
                if (process.HasExited)
                    throw new InvalidOperationException(
                        $"The application exited during startup (exit code {process.ExitCode}).");
                var window = application.GetMainWindow(automation, TimeSpan.Zero);
                return window is not null && !string.IsNullOrEmpty(window.Title) ? window : null;
            },
            timeout,
            "main window of the application under test");
    }

    /// <summary>Ends the application by process id. Never throws, so it cannot hide the exception that made the
    /// caller clean up.</summary>
    private static void Kill(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Already exited (ArgumentException: no such process) or exiting right now
        }
    }
}
