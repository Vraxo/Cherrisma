namespace Cherris;

public abstract class ClickableRectangle : Clickable
{
    public override bool IsMouseOver()
    {
        var owningWindowNode = GetOwningWindowNode();
        Vector2 mousePosition;

        if (owningWindowNode != null)
        {
            // If part of a WindowNode, use its local mouse position
            mousePosition = owningWindowNode.LocalMousePosition;
        }
        else
        {
            // Otherwise, assume it's in the MainAppWindow
            var mainAppWindow = ApplicationServer.Instance.GetMainAppWindow();
            if (mainAppWindow != null)
            {
                mousePosition = mainAppWindow.GetLocalMousePosition();
            }
            else
            {
                // Fallback if main window isn't available (should not happen in normal operation)
                Log.Warning($"ClickableRectangle '{Name}': MainAppWindow not found for mouse position query. Using global Input.MousePosition as fallback.");
                mousePosition = Input.MousePosition;
            }
        }

        var globalPos = GlobalPosition;
        var origin = Origin;
        var size = ScaledSize;

        float left = globalPos.X - origin.X;
        float top = globalPos.Y - origin.Y;
        float right = left + size.X;
        float bottom = top + size.Y;

        bool isMouseOver =
            mousePosition.X >= left &&
            mousePosition.X < right &&
            mousePosition.Y >= top &&
            mousePosition.Y < bottom;

        return isMouseOver;
    }
}