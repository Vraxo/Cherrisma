using Raylib_cs;

namespace Cherris;

public sealed class RenderServer
{
    private static RenderServer? _instance;
    public static RenderServer Instance => _instance ??= new();

    public Camera? Camera;
    public Shader? PostProcessingShader { get; set; }

    private readonly List<DrawCommand> drawCommands = [];
    private RenderTexture2D renderTexture;

    // Main

    private RenderServer()
    {
        var mainWin = ApplicationServer.Instance.GetMainAppWindow();
        Vector2 windowSize = mainWin != null ? new Vector2(mainWin.Width, mainWin.Height) : new Vector2(800, 600); // Default fallback
        renderTexture = Raylib.LoadRenderTexture((int)windowSize.X, (int)windowSize.Y);

        // DisplayServer.Instance.WindowSizeChanged -= WindowSizeChanged; // Event is removed from DisplayServer
        // The logic for resizing renderTexture needs a new mechanism if used with multiple/dynamic window sizes.
        // For now, it's initialized with main window size and won't resize automatically based on DisplayServer events.

        //PostProcessingShader = Shader.Load(null, "Res/Shaders/Bloom.fs");
    }

    // This method is no longer automatically called by DisplayServer event.
    // If renderTexture needs to resize, another mechanism is required.
    private void WindowSizeChanged(Vector2 newSize)
    {
        Raylib.UnloadRenderTexture(renderTexture); // Unload old texture
        renderTexture = Raylib.LoadRenderTexture(
            (int)newSize.X,
            (int)newSize.Y);
    }

    public void Process()
    {
        Raylib.BeginTextureMode(renderTexture);
        //Raylib.ClearBackground(Color.DarkGray);
        BeginCameraMode();
        ProcessDrawCommands();
        EndCameraMode();
        Raylib.EndTextureMode();

        BeginShaderMode(PostProcessingShader);
        //Rectangle source = new(0, 0, renderTexture.Texture.Width, -renderTexture.Texture.Height);
        //Raylib.DrawTextureRec(renderTexture.Texture, source, Vector2.Zero, Color.White);
        EndShaderMode();
    }

    public void Process2()
    {
        //Raylib.ClearBackground(Color.DarkGray);
        //BeginShaderMode(PostProcessingShader);
        //    BeginCameraMode();
        //        ProcessDrawCommands();
        //    EndCameraMode();
        //EndShaderMode();
    }

    public void Submit(Action drawAction, int layer)
    {
        drawCommands.Add(new(drawAction, layer));
    }

    public Vector2 GetScreenToWorld(Vector2 position)
    {
        return Camera is null
            ? position
            : Raylib.GetScreenToWorld2D(position, Camera);
    }

    public Vector2 GetWorldToScreen(Vector2 position)
    {
        return Camera is null
            ? position
            : Raylib.GetWorldToScreen2D(position, Camera);
    }

    // Scissor mode

    public static void BeginScissorMode(Vector2 position, Vector2 size)
    {
        Raylib.BeginScissorMode(
            (int)position.X,
            (int)position.Y,
            (int)size.X,
            (int)size.Y);
    }

    public static void EndScissorMode()
    {
        Raylib.EndScissorMode();
    }

    // Camera

    public void SetCamera(Camera camera)
    {
        Camera = camera;
    }

    private void BeginCameraMode()
    {
        if (Camera is null)
        {
            return;
        }

        // Use the Camera's owning window size for offset
        Vector2 windowSize = Camera.GetWindowSizeV2();

        Camera2D cam = new()
        {
            Target = Camera.GlobalPosition,
            Offset = windowSize / 2,
            Zoom = Camera.Zoom,
        };

        Raylib.BeginMode2D(cam);
    }

    private void EndCameraMode()
    {
        if (Camera is null)
        {
            return;
        }

        Raylib.EndMode2D();
    }

    // Shader mode

    public static void BeginShaderMode(Shader? shader)
    {
        if (shader is null)
        {
            return;
        }

        Raylib.BeginShaderMode(shader);
    }

    public static void EndShaderMode()
    {
        Raylib.EndShaderMode();
    }

    // Other

    private void ProcessDrawCommands()
    {
        foreach (DrawCommand command in drawCommands.OrderBy(c => c.Layer))
        {
            command.DrawAction.Invoke();
        }

        drawCommands.Clear();
    }

    private class DrawCommand(Action drawAction, int layer)
    {
        public Action DrawAction { get; } = drawAction;
        public int Layer { get; } = layer;
    }
}