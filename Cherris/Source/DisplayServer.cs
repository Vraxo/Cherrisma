namespace Cherris;

public sealed class DisplayServer
{
    private static DisplayServer? _instance;
    public static DisplayServer Instance => _instance ??= new();

    // Public

    public DisplayServer()
    {
        // Constructor remains if any future non-Raylib global display settings are needed.
    }

    // All Raylib-specific methods and properties have been removed.
    // Window size and mouse position are now handled by individual window instances (Win32Window derived classes)
    // and can be queried via Node.GetOwningWindow() and then window.Width/Height or window.GetLocalMousePosition().
}