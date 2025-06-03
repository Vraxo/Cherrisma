using Vortice.Mathematics; // For Rect

namespace Cherris;

public class VSlider : Slider
{
    public VSliderDirection Direction { get; set; } = VSliderDirection.TopToBottom;

    public VSlider()
    {
        Size = new(16, 200);
        GrabberSize = new(24, 12);
    }
    protected override void CalculateTrackBounds()
    {
        // This method sets fields (this.trackPosition, trackMin, trackMax) used by input handling logic in Process().
        Vector2 currentGlobalPos = GlobalPosition - Origin; // VSlider uses Origin in base.Process -> Clickable -> Node2D
                                                            // However, Slider.DrawBackground uses GlobalPosition directly.
                                                            // For consistency with how HSlider works and drawing, let's use GlobalPosition.
        currentGlobalPos = GlobalPosition; // Use raw GlobalPosition for track bounds, consistent with Slider.DrawBackground
        this.trackPosition = currentGlobalPos;
        trackMin = currentGlobalPos.Y;
        trackMax = currentGlobalPos.Y + Size.Y;
    }

    protected override void UpdateHoverStates()
    {
        Vector2 mousePos = GetLocalMousePosition();
        Vector2 currentTrackPosForHover = this.trackPosition; // Use trackPosition from Process cycle

        trackHovered = mousePos.X >= currentTrackPosForHover.X &&
                       mousePos.X <= currentTrackPosForHover.X + Size.X &&
                       mousePos.Y >= currentTrackPosForHover.Y &&
                       mousePos.Y <= currentTrackPosForHover.Y + Size.Y;

        Vector2 grabberTopLeftPos = CalculateGrabberPosition(); // Will use fresh GlobalPosition
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
            if (trackHovered) // Uses this.trackPosition consistent with current Process() cycle
            {
                float localMouseY = GetLocalMousePosition().Y;
                float clampedMouseY = Math.Clamp(localMouseY, this.trackPosition.Y, this.trackPosition.Y + Size.Y);
                Value = ConvertPositionToValue(clampedMouseY); // Uses this.trackPosition
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
        float localMouseY = GetLocalMousePosition().Y;
        float clampedMouseY = Math.Clamp(localMouseY, this.trackPosition.Y, this.trackPosition.Y + Size.Y);
        Value = ConvertPositionToValue(clampedMouseY); // Uses this.trackPosition
    }

    private void HandleMouseWheel()
    {
        if (!trackHovered && !grabberHovered) return;

        float wheelDelta = Input.GetMouseWheelMovement();
        if (wheelDelta == 0) return;

        Value = ApplyStep(Value + (wheelDelta * Step * (Direction == VSliderDirection.TopToBottom ? 1 : -1)));
        PlaySound();
    }

    protected override float ConvertPositionToValue(float positionOnTrack)
    {
        // Uses this.trackPosition.Y, which is updated in CalculateTrackBounds within the same Process() cycle.
        float effectiveTrackMin = this.trackPosition.Y;
        float effectiveTrackHeight = Size.Y; // Size.Y is dynamic

        if (effectiveTrackHeight <= 0) return MinValue;

        float normalized = (positionOnTrack - effectiveTrackMin) / effectiveTrackHeight;
        normalized = Math.Clamp(normalized, 0f, 1f);

        if (Direction == VSliderDirection.BottomToTop)
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

        float foregroundHeight = Size.Y * fillRatio; // Size.Y is dynamic
        Rect foregroundRect;

        if (Direction == VSliderDirection.BottomToTop)
        {
            foregroundRect = new Rect(
                currentGlobalPos.X,
                currentGlobalPos.Y + Size.Y - foregroundHeight,
                Size.X,
                foregroundHeight
            );
        }
        else // TopToBottom
        {
            foregroundRect = new Rect(
                currentGlobalPos.X,
                currentGlobalPos.Y,
                Size.X,
                foregroundHeight
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

        if (Direction == VSliderDirection.BottomToTop)
        {
            normalizedValue = 1f - normalizedValue;
        }

        float grabberCenterY = currentGlobalPos.Y + normalizedValue * Size.Y;
        float grabberTopY = grabberCenterY - GrabberSize.Y / 2f;
        grabberTopY = Math.Clamp(grabberTopY, currentGlobalPos.Y, currentGlobalPos.Y + Size.Y - GrabberSize.Y);

        float xPos = currentGlobalPos.X + (Size.X / 2f) - GrabberSize.X / 2f;
        return new Vector2(xPos, grabberTopY);
    }

    protected override void UpdateGrabberThemeVisuals()
    {
        // This method uses grabberPressed, Focused, grabberHovered which are updated in Process()
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
        if (Input.IsActionPressed("UiUp"))
        {
            Value = ApplyStep(Value + Step * (Direction == VSliderDirection.TopToBottom ? -1 : 1));
            PlaySound();
        }
        else if (Input.IsActionPressed("UiDown"))
        {
            Value = ApplyStep(Value - Step * (Direction == VSliderDirection.TopToBottom ? -1 : 1));
            PlaySound();
        }
    }
}