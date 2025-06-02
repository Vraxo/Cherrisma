using System.Reflection;
using YamlDotNet.Serialization;
using System.Diagnostics;
using System.Threading;

namespace Cherris;

public sealed class ApplicationServer
{
    private static readonly Lazy<ApplicationServer> lazyInstance = new(() => new ApplicationServer());
    private MainAppWindow? mainWindow;
    private Configuration? applicationConfig;
    private readonly List<SecondaryWindow> secondaryWindows = new();

    private const string ConfigFilePath = "Res/Cherris/Config.yaml";
    private const string LogFilePath = "Res/Cherris/Log.txt";

    private Stopwatch gameLoopStopwatch = new Stopwatch();
    private Thread? gameLogicThread;
    private volatile bool _isRunning = false;


    public static ApplicationServer Instance => lazyInstance.Value;

    private ApplicationServer()
    {

    }

    public IntPtr GetMainWindowHandle()
    {
        return mainWindow?.Handle ?? IntPtr.Zero;
    }

    public MainAppWindow? GetMainAppWindow()
    {
        return mainWindow;
    }

    public void Run()
    {
        if (!Start())
        {
            Log.Error("ApplicationCore failed to start.");
            return;
        }

        if (mainWindow is null)
        {
            Log.Error("Main window was not initialized.");
            return;
        }

        _isRunning = true;
        gameLogicThread = new Thread(GameLogicLoop) { IsBackground = true, Name = "GameLogicThread" };
        gameLogicThread.Start();

        UIThreadLoop();

        Log.Info("UI loop exited. Signaling game logic thread to stop.");
        _isRunning = false;
        gameLogicThread?.Join();

        Log.Info("Application exiting.");
        Cleanup();
    }

    private bool Start()
    {
        CreateLogFile();
        SetCurrentDirectory();

        applicationConfig = LoadConfig();
        if (applicationConfig is null)
        {
            Log.Error("Failed to load configuration.");
            return false;
        }

        try
        {
            mainWindow = new MainAppWindow(
                applicationConfig.Title,
                applicationConfig.Width,
                applicationConfig.Height);

            if (!mainWindow.TryCreateWindow())
            {
                Log.Error("Failed to create main window.");
                return false;
            }

            mainWindow.Closed += OnMainWindowClosed;

            ApplyConfig();

            if (!mainWindow.InitializeWindowAndGraphics())
            {
                Log.Error("Failed to initialize window graphics.");
                return false;
            }

            mainWindow.ShowWindow();
            mainWindow.Invalidate(); // Ensure an initial paint request
        }
        catch (Exception ex)
        {
            Log.Error($"Error during window initialization: {ex.Message}");
            return false;
        }

        return true;
    }

    private void GameLogicLoop()
    {
        Log.Info("GameLogicThread started.");
        gameLoopStopwatch.Start();
        long lastFrameTicks = gameLoopStopwatch.ElapsedTicks;

        while (_isRunning)
        {
            long currentFrameTicks = gameLoopStopwatch.ElapsedTicks;
            float deltaSeconds = (float)(currentFrameTicks - lastFrameTicks) / Stopwatch.Frequency;
            lastFrameTicks = currentFrameTicks;

            Time.Delta = Math.Max(1e-5f, deltaSeconds);
            if (Time.Delta > 0.1f) Time.Delta = 0.1f;

            lock (SceneTree.Instance.SyncRoot)
            {
                ClickServer.Instance.Process();
                SceneTree.Instance.Process();
            }

            Input.Update();

            int sleepTime = (int)(((1.0f / 60.0f) - Time.Delta) * 1000.0f);
            if (sleepTime > 0)
            {
                Thread.Sleep(sleepTime);
            }
            else
            {
                Thread.Sleep(1);
            }
        }
        gameLoopStopwatch.Stop();
        Log.Info("GameLogicThread stopped.");
    }

    private void UIThreadLoop()
    {
        while (mainWindow != null && mainWindow.IsOpen)
        {
            ProcessSystemMessages();

            // Main window rendering is driven by WM_PAINT (from InvalidateRect or OS)
            // Secondary window rendering can also be driven by their WM_PAINT
            // If we need to force repaint secondary windows, we'd call Invalidate() on them.
            RenderSecondaryWindows();
        }
    }


    private void ProcessSystemMessages()
    {
        while (NativeMethods.PeekMessage(out NativeMethods.MSG msg, IntPtr.Zero, 0, 0, NativeMethods.PM_REMOVE))
        {
            if (msg.message == NativeMethods.WM_QUIT)
            {
                Log.Info("WM_QUIT received, signaling application close.");
                _isRunning = false;
                break;
            }

            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessage(ref msg);
        }
    }

    private void RenderSecondaryWindows()
    {
        List<SecondaryWindow> windowsToRenderSnapshot;
        lock (secondaryWindows)
        {
            windowsToRenderSnapshot = new List<SecondaryWindow>(secondaryWindows);
        }

        foreach (SecondaryWindow window in windowsToRenderSnapshot)
        {
            if (window.IsOpen)
            {
                // window.Invalidate(); // If needed to force repaint
            }
        }
    }

    private void OnMainWindowClosed()
    {
        Log.Info("Main window closed signal received (via mainWindow.Closed event). Setting _isRunning to false.");
        _isRunning = false;
        CloseAllSecondaryWindows();
    }

    private void Cleanup()
    {
        Log.Info("ApplicationCore Cleanup starting.");
        CloseAllSecondaryWindows();
        mainWindow?.Dispose();
        mainWindow = null;
        Log.Info("ApplicationCore Cleanup finished.");
    }

    private void CloseAllSecondaryWindows()
    {
        List<SecondaryWindow> windowsToCloseSnapshot;
        lock (secondaryWindows)
        {
            windowsToCloseSnapshot = new List<SecondaryWindow>(secondaryWindows);
        }
        foreach (var window in windowsToCloseSnapshot)
        {
            if (window.IsOpen)
            {
                window.Close();
            }
        }
    }

    internal void RegisterSecondaryWindow(SecondaryWindow window)
    {
        lock (secondaryWindows)
        {
            if (!secondaryWindows.Contains(window))
            {
                secondaryWindows.Add(window);
                Log.Info($"Registered secondary window: {window.Title}");
            }
        }
    }

    internal void UnregisterSecondaryWindow(SecondaryWindow window)
    {
        lock (secondaryWindows)
        {
            if (secondaryWindows.Remove(window))
            {
                Log.Info($"Unregistered secondary window: {window.Title}");
            }
        }
    }

    private static void SetRootNodeFromConfig(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath))
        {
            Log.Warning("MainScenePath is not defined in the configuration.");
            return;
        }

        try
        {
            var packedScene = new PackedScene(scenePath);
            SceneTree.Instance.RootNode = packedScene.Instantiate<Node>();
            Log.Info($"Loaded main scene: {scenePath}");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to load main scene '{scenePath}': {ex.Message}");
            SceneTree.Instance.RootNode = new Node { Name = "ErrorRoot" };
        }
    }

    private static void CreateLogFile()
    {
        try
        {
            string? logDirectory = Path.GetDirectoryName(LogFilePath);
            if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            if (File.Exists(LogFilePath))
            {
                File.Delete(LogFilePath);
            }

            using (File.Create(LogFilePath)) { }
            Log.Info($"Log file created at {Path.GetFullPath(LogFilePath)}");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FATAL] Failed to create log file '{LogFilePath}': {ex.Message}");
            Console.ResetColor();
        }
    }

    private static void SetCurrentDirectory()
    {
        try
        {
            string? assemblyLocation = Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrEmpty(assemblyLocation))
            {
                Log.Warning("Could not get assembly location. Current directory not changed.");
                return;
            }

            string? directoryName = Path.GetDirectoryName(assemblyLocation);
            if (string.IsNullOrEmpty(directoryName))
            {
                Log.Warning($"Could not get directory name from assembly location: {assemblyLocation}. Current directory not changed.");
                return;
            }

            Environment.CurrentDirectory = directoryName;
            Log.Info($"Current directory set to: {directoryName}");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to set current directory: {ex.Message}");
        }
    }

    private Configuration? LoadConfig()
    {
        if (!File.Exists(ConfigFilePath))
        {
            Log.Error($"Configuration file not found: {ConfigFilePath}");
            return null;
        }

        try
        {
            var deserializer = new DeserializerBuilder().Build();
            string yaml = File.ReadAllText(ConfigFilePath);
            var config = deserializer.Deserialize<Configuration>(yaml);
            Log.Info("Configuration loaded successfully.");
            return config;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to load or parse configuration file '{ConfigFilePath}': {ex.Message}");
            return null;
        }
    }

    private void ApplyConfig()
    {
        if (applicationConfig == null)
        {
            Log.Error("Cannot apply configuration because it was not loaded.");
            return;
        }

        if (mainWindow != null)
        {
            mainWindow.VSyncEnabled = applicationConfig.VSync;
            mainWindow.BackdropType = applicationConfig.BackdropType;
        }

        SetRootNodeFromConfig(applicationConfig.MainScenePath);
    }
}