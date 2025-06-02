namespace Cherris;

public class MainAppWindow : Direct2DAppWindow
{
    public event Action? Closed;
    private bool _firstDrawLogged = false;
    private Vector2 _mainCurrentMousePosition = Vector2.Zero;

    private const uint SIZEMOVE_TIMER_ID = 1;
    private const uint SIZEMOVE_TIMER_INTERVAL = 16; // Milliseconds, roughly 60 FPS

    public MainAppWindow(string title = "My DirectUI App", int width = 800, int height = 600)
        : base(title, width, height)
    {
        Input.SetupDefaultActions();
    }

    public Vector2 GetLocalMousePosition() => _mainCurrentMousePosition;

    protected override void DrawUIContent(DrawingContext context)
    {
        if (!_firstDrawLogged)
        {
            Log.Info($"MainAppWindow.DrawUIContent called for '{Title}'. Rendering SceneTree.");
            _firstDrawLogged = true;
        }

        lock (SceneTree.Instance.SyncRoot)
        {
            SceneTree.Instance.RenderScene(context);
        }
    }

    protected override bool OnClose()
    {
        Log.Info($"MainAppWindow '{Title}' OnClose called. Invoking Closed event.");
        Closed?.Invoke();
        return base.OnClose();
    }

    protected override void Cleanup()
    {
        Log.Info($"MainAppWindow '{Title}' Cleanup starting.");
        if (Handle != IntPtr.Zero)
        {
            NativeMethods.KillTimer(Handle, (IntPtr)SIZEMOVE_TIMER_ID);
        }
        base.Cleanup();
        Log.Info($"MainAppWindow '{Title}' Cleanup finished.");
    }

    protected override IntPtr HandleMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case NativeMethods.WM_WINDOWPOSCHANGING:
                // RDW_UPDATENOW: Causes the window to receive a WM_PAINT message immediately.
                // RDW_INVALIDATE: Invalidates the entire client area.
                // RDW_INTERNALPAINT: Causes WM_PAINT to be processed even if the window is not visible.
                // RDW_ERASE could also be added if background erase is desired before paint, but D2D usually handles clearing.
                NativeMethods.RedrawWindow(hWnd, IntPtr.Zero, IntPtr.Zero, NativeMethods.RDW_UPDATENOW | NativeMethods.RDW_INVALIDATE | NativeMethods.RDW_INTERNALPAINT);
                // Log.Info($"WM_WINDOWPOSCHANGING for '{Title}': RedrawWindow called.");
                break;

            case NativeMethods.WM_ENTERSIZEMOVE:
                Log.Info($"WM_ENTERSIZEMOVE for '{Title}': Starting UI update timer.");
                NativeMethods.SetTimer(hWnd, (IntPtr)SIZEMOVE_TIMER_ID, SIZEMOVE_TIMER_INTERVAL, IntPtr.Zero);
                return IntPtr.Zero;

            case NativeMethods.WM_EXITSIZEMOVE:
                Log.Info($"WM_EXITSIZEMOVE for '{Title}': Killing UI update timer.");
                NativeMethods.KillTimer(hWnd, (IntPtr)SIZEMOVE_TIMER_ID);

                NativeMethods.RedrawWindow(hWnd, IntPtr.Zero, IntPtr.Zero, NativeMethods.RDW_UPDATENOW | NativeMethods.RDW_INVALIDATE | NativeMethods.RDW_INTERNALPAINT);
                Log.Info($"WM_EXITSIZEMOVE for '{Title}': Final RedrawWindow requested.");
                return IntPtr.Zero;

            case NativeMethods.WM_TIMER:
                if (wParam == (IntPtr)SIZEMOVE_TIMER_ID)
                {
                    NativeMethods.RedrawWindow(hWnd, IntPtr.Zero, IntPtr.Zero, NativeMethods.RDW_UPDATENOW | NativeMethods.RDW_INVALIDATE | NativeMethods.RDW_INTERNALPAINT);
                    // Log.Info($"WM_TIMER for '{Title}': RedrawWindow() called during resize/move.");
                    return IntPtr.Zero;
                }
                break;

            case NativeMethods.WM_MOUSEMOVE:
                int x = NativeMethods.GET_X_LPARAM(lParam);
                int y = NativeMethods.GET_Y_LPARAM(lParam);
                _mainCurrentMousePosition = new Vector2(x, y);
                Input.UpdateMousePosition(_mainCurrentMousePosition);
                return base.HandleMessage(hWnd, msg, wParam, lParam);


            case NativeMethods.WM_LBUTTONDOWN:
                Input.UpdateMouseButton(MouseButtonCode.Left, true);
                return base.HandleMessage(hWnd, msg, wParam, lParam);
            case NativeMethods.WM_LBUTTONUP:
                Input.UpdateMouseButton(MouseButtonCode.Left, false);
                return base.HandleMessage(hWnd, msg, wParam, lParam);

            case NativeMethods.WM_RBUTTONDOWN:
                Input.UpdateMouseButton(MouseButtonCode.Right, true);
                return base.HandleMessage(hWnd, msg, wParam, lParam);
            case NativeMethods.WM_RBUTTONUP:
                Input.UpdateMouseButton(MouseButtonCode.Right, false);
                return base.HandleMessage(hWnd, msg, wParam, lParam);

            case NativeMethods.WM_MBUTTONDOWN:
                Input.UpdateMouseButton(MouseButtonCode.Middle, true);
                return base.HandleMessage(hWnd, msg, wParam, lParam);
            case NativeMethods.WM_MBUTTONUP:
                Input.UpdateMouseButton(MouseButtonCode.Middle, false);
                return base.HandleMessage(hWnd, msg, wParam, lParam);

            case NativeMethods.WM_XBUTTONDOWN:
                int xButton1 = NativeMethods.GET_XBUTTON_WPARAM(wParam);
                if (xButton1 == NativeMethods.XBUTTON1) Input.UpdateMouseButton(MouseButtonCode.Side, true);
                if (xButton1 == NativeMethods.XBUTTON2) Input.UpdateMouseButton(MouseButtonCode.Extra, true);
                return base.HandleMessage(hWnd, msg, wParam, lParam);
            case NativeMethods.WM_XBUTTONUP:
                int xButton2 = NativeMethods.GET_XBUTTON_WPARAM(wParam);
                if (xButton2 == NativeMethods.XBUTTON1) Input.UpdateMouseButton(MouseButtonCode.Side, false);
                if (xButton2 == NativeMethods.XBUTTON2) Input.UpdateMouseButton(MouseButtonCode.Extra, false);
                return base.HandleMessage(hWnd, msg, wParam, lParam);

            case NativeMethods.WM_MOUSEWHEEL:
                short wheelDelta = NativeMethods.GET_WHEEL_DELTA_WPARAM(wParam);
                Input.UpdateMouseWheel((float)wheelDelta / NativeMethods.WHEEL_DELTA);
                return base.HandleMessage(hWnd, msg, wParam, lParam);

            case NativeMethods.WM_KEYDOWN:
            case NativeMethods.WM_SYSKEYDOWN:
                int vkCodeDown = (int)wParam;
                if (Enum.IsDefined(typeof(KeyCode), vkCodeDown))
                {
                    Input.UpdateKey((KeyCode)vkCodeDown, true);
                }
                return base.HandleMessage(hWnd, msg, wParam, lParam);

            case NativeMethods.WM_KEYUP:
            case NativeMethods.WM_SYSKEYUP:
                int vkCodeUp = (int)wParam;
                if (Enum.IsDefined(typeof(KeyCode), vkCodeUp))
                {
                    Input.UpdateKey((KeyCode)vkCodeUp, false);
                }
                return base.HandleMessage(hWnd, msg, wParam, lParam);

            case NativeMethods.WM_DESTROY:
                return base.HandleMessage(hWnd, msg, wParam, lParam);

        }

        return base.HandleMessage(hWnd, msg, wParam, lParam);
    }
}