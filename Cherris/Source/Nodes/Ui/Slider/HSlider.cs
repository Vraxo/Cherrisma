using Vortice.Mathematics; // For Rect

namespace Cherris;

public class HSlider : Slider
{
    public HSliderDirection Direction { get; set; } = HSliderDirection.LeftToRight;

    protected override void CalculateTrackBounds()
    {
        // This method sets fields (this.trackPosition, trackMin, trackMax) used by input handling logic in Process().
        Vector2 currentGlobalPos = GlobalPosition; // Get current global position for input logic bounds
        this.trackPosition = currentGlobalPos;
        trackMin = currentGlobalPos.X;
        // If the grabber must stay fully within track visually (for mouse interaction):
        // trackMax = currentGlobalPos.X + Size.X - GrabberSize.X; (This might be too restrictive for value calculation)
        // Let ConvertPositionToValue and CalculateGrabberPosition handle clamping based on full Size.X range.
        trackMax = currentGlobalPos.X + Size.X;
    }

    protected override void UpdateHoverStates()
    {
        Vector2 mousePos = GetLocalMousePosition();
        Vector2 currentTrackPosForHover = this.trackPosition; // Use trackPosition from Process cycle for hover logic consistency

        // Track hover: considers the whole Size of the slider, relative to its top-left (currentTrackPosForHover)
        trackHovered = mousePos.X >= currentTrackPosForHover.X &&
                       mousePos.X <= currentTrackPosForHover.X + Size.X &&
                       mousePos.Y >= currentTrackPosForHover.Y &&
                       mousePos.Y <= currentTrackPosForHover.Y + Size.Y;

        // Grabber hover: uses actual grabber bounds. CalculateGrabberPosition will use fresh GlobalPosition.
        Vector2 grabberTopLeftPos = CalculateGrabberPosition();
        grabberHovered = mousePos.X >= grabberTopLeftPos.X &&
                         mousePos.X <= grabberTopLeftPos.X + GrabberSize.X &&
                         mousePos.Y >= grabberTopLeftPos.Y &&
                         mousePos.Y <= grabberTopLeftPos.Y + GrabberSize.Y;
    }

    protected override void HandleInput()
    {
        HandleMousePress();
        HandleMouseDrag();
        HandleMouseWheel();
    }

    private void HandleMousePress()
    {
        if (Input.IsMouseButtonPressed(MouseButtonCode.Left))
        {
            // Mouse position is already local to the window.
            // trackHovered uses this.trackPosition which is up-to-date for the current Process() cycle.
            if (trackHovered)
            {
                float localMouseX = GetLocalMousePosition().X;
                // Clamp mouse X to be within the visual track extents for value calculation,
                // using this.trackPosition which is consistent with trackHovered check.
                float clampedMouseXOnTrack = Math.Clamp(localMouseX, this.trackPosition.X, this.trackPosition.X + Size.X);
                Value = ConvertPositionToValue(clampedMouseXOnTrack); // ConvertPositionToValue also uses this.trackPosition
                grabberPressed = true;
                PlaySound();
            }
        }
        else if (Input.IsMouseButtonReleased(MouseButtonCode.Left))
        {
            grabberPressed = false;
        }
    }

    private void HandleMouseDrag()
    {
        if (!grabberPressed) return;

        if (Input.IsMouseButtonReleased(MouseButtonCode.Left))
        {
            grabberPressed = false;
            return;
        }

        float localMouseX = GetLocalMousePosition().X;
        // Use this.trackPosition for consistency with press logic.
        float clampedMouseXOnTrack = Math.Clamp(localMouseX, this.trackPosition.X, this.trackPosition.X + Size.X);
        Value = ConvertPositionToValue(clampedMouseXOnTrack);
    }

    private void HandleMouseWheel()
    {
        if (!trackHovered && !grabberHovered) return;

        float wheelDelta = Input.GetMouseWheelMovement();
        if (wheelDelta == 0) return;

        Value = ApplyStep(Value + (wheelDelta * Step * (Direction == HSliderDirection.LeftToRight ? 1 : -1)));
        PlaySound();
    }

    protected override float ConvertPositionToValue(float positionOnTrackInLocalSpace)
    {
        // positionOnTrackInLocalSpace is a local X coordinate (e.g., mouse.X)
        // This uses this.trackPosition.X, which is updated in CalculateTrackBounds within the same Process() cycle.
        float effectiveTrackMin = this.trackPosition.X;
        float effectiveTrackWidth = Size.X; // Size.X is dynamic and up-to-date

        if (effectiveTrackWidth <= 0) return MinValue;

        float normalized = (positionOnTrackInLocalSpace - effectiveTrackMin) / effectiveTrackWidth;
        normalized = Math.Clamp(normalized, 0f, 1f);

        if (Direction == HSliderDirection.RightToLeft)
        {
            normalized = 1f - normalized;
        }

        float rawValue = MinValue + normalized * (MaxValue - MinValue);
        return ApplyStep(rawValue);
    }

    protected override void DrawForeground(DrawingContext context)
    {
        Vector2 currentGlobalPos = GlobalPosition; // Use fresh GlobalPosition for drawing
        float range = MaxValue - MinValue;
        float fillRatio = (range == 0) ? 0.0f : (this.Value - MinValue) / range;
        fillRatio = Math.Clamp(fillRatio, 0f, 1f);

        float foregroundWidth = Size.X * fillRatio; // Size.X is dynamic
        Rect foregroundRect;

        if (Direction == HSliderDirection.RightToLeft)
        {
            foregroundRect = new Rect(
                currentGlobalPos.X + Size.X - foregroundWidth,
                currentGlobalPos.Y,
                foregroundWidth,
                Size.Y
            );
        }
        else // LeftToRight
        {
            foregroundRect = new Rect(
                currentGlobalPos.X,
                currentGlobalPos.Y,
                foregroundWidth,
                Size.Y
            );
        }

        if (foregroundRect.Width > 0 && foregroundRect.Height > 0)
        {
            DrawStyledRectangle(context, foregroundRect, Style.Foreground);
        }
    }

    protected override Vector2 CalculateGrabberPosition()
    {
        Vector2 currentGlobalPos = GlobalPosition; // Use fresh GlobalPosition for drawing related calculations
        float range = MaxValue - MinValue;
        float normalizedValue = (range == 0) ? 0.0f : (this.Value - MinValue) / range;
        normalizedValue = Math.Clamp(normalizedValue, 0f, 1f);

        if (Direction == HSliderDirection.RightToLeft)
        {
            normalizedValue = 1f - normalizedValue;
        }

        float grabberCenterX = currentGlobalPos.X + normalizedValue * Size.X;
        float grabberLeftX = grabberCenterX - GrabberSize.X / 2f;
        grabberLeftX = Math.Clamp(grabberLeftX, currentGlobalPos.X, currentGlobalPos.X + Size.X - GrabberSize.X);

        float yPos = currentGlobalPos.Y + (Size.Y / 2f) - GrabberSize.Y / 2f;
        return new Vector2(grabberLeftX, yPos);
    }

    protected override void UpdateGrabberThemeVisuals()
    {
        // This method uses grabberPressed, Focused, grabberHovered which are updated in Process()
        // So, their visual state update in relation to theme will be based on the Process() cycle.
        if (Disabled)
        {
            Style.Grabber.Current = Style.Grabber.Disabled;
            return;
        }

        if (grabberPressed)
        {
            Style.Grabber.Current = Style.Grabber.Pressed;
        }
        else if (Focused)
        {
            Style.Grabber.Current = grabberHovered ? Style.Grabber.Hover : Style.Grabber.Focused;
        }
        else if (grabberHovered)
        {
            Style.Grabber.Current = Style.Grabber.Hover;
        }
        else
        {
            Style.Grabber.Current = Style.Grabber.Normal;
        }
    }

    protected override void HandleKeyboardNavigation()
    {
        if (Input.IsActionPressed("UiLeft"))
        {
            Value = ApplyStep(Value - Step * (Direction == HSliderDirection.LeftToRight ? 1 : -1));
            PlaySound();
        }
        else if (Input.IsActionPressed("UiRight"))
        {
            Value = ApplyStep(Value + Step * (Direction == HSliderDirection.LeftToRight ? 1 : -1));
            PlaySound();
        }
    }
}