using Vortice.Mathematics; // For Rect

namespace Cherris;

public class VSlider : Slider
{
    public VSliderDirection Direction { get; set; } = VSliderDirection.TopToBottom;

    public VSlider() // VSlider specific defaults
    {
        Size = new(16, 200); // Default tall and thin
        GrabberSize = new(24, 12); // Wider than tall for VSlider grabber
    }
    protected override void CalculateTrackBounds()
    {
        trackPosition = GlobalPosition - Origin;
        trackMin = trackPosition.Y;
        trackMax = trackPosition.Y + Size.Y;
    }

    protected override void UpdateHoverStates()
    {
        Vector2 mousePos = GetLocalMousePosition();

        trackHovered = mousePos.X >= trackPosition.X &&
                       mousePos.X <= trackPosition.X + Size.X &&
                       mousePos.Y >= trackPosition.Y &&
                       mousePos.Y <= trackPosition.Y + Size.Y;

        Vector2 grabberCenterPos = CalculateGrabberPosition();
        grabberHovered = mousePos.X >= grabberCenterPos.X &&
                         mousePos.X <= grabberCenterPos.X + GrabberSize.X &&
                         mousePos.Y >= grabberCenterPos.Y &&
                         mousePos.Y <= grabberCenterPos.Y + GrabberSize.Y;
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
            if (trackHovered)
            {
                float clampedMouseY = Math.Clamp(GetLocalMousePosition().Y, trackPosition.Y, trackPosition.Y + Size.Y);
                Value = ConvertPositionToValue(clampedMouseY);
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

        float clampedMouseY = Math.Clamp(GetLocalMousePosition().Y, trackPosition.Y, trackPosition.Y + Size.Y);
        Value = ConvertPositionToValue(clampedMouseY);
    }

    private void HandleMouseWheel()
    {
        if (!trackHovered && !grabberHovered) return;

        float wheelDelta = Input.GetMouseWheelMovement();
        if (wheelDelta == 0) return;

        // Positive wheelDelta for VSlider usually means increase value (scroll "down" the list, "up" the value)
        Value = ApplyStep(Value + (wheelDelta * Step * (Direction == VSliderDirection.TopToBottom ? 1 : -1)));
        PlaySound();
    }

    protected override float ConvertPositionToValue(float positionOnTrack)
    {
        float effectiveTrackMin = trackPosition.Y;
        float effectiveTrackHeight = Size.Y;

        if (effectiveTrackHeight <= 0) return MinValue;

        float normalized = (positionOnTrack - effectiveTrackMin) / effectiveTrackHeight;
        normalized = Math.Clamp(normalized, 0f, 1f);

        if (Direction == VSliderDirection.BottomToTop) // For BottomToTop, higher Y means lower value
        {
            normalized = 1f - normalized;
        }
        // For TopToBottom, higher Y means higher value (already handled by default normalization)


        float rawValue = MinValue + normalized * (MaxValue - MinValue);
        return ApplyStep(rawValue);
    }

    protected override void DrawForeground(DrawingContext context)
    {
        float range = MaxValue - MinValue;
        float fillRatio = (range == 0) ? 0.0f : (this.Value - MinValue) / range;
        fillRatio = Math.Clamp(fillRatio, 0f, 1f);

        float foregroundHeight = Size.Y * fillRatio;
        Rect foregroundRect;

        if (Direction == VSliderDirection.BottomToTop)
        {
            foregroundRect = new Rect(
                trackPosition.X,
                trackPosition.Y + Size.Y - foregroundHeight,
                Size.X,
                foregroundHeight
            );
        }
        else // TopToBottom
        {
            foregroundRect = new Rect(
                trackPosition.X,
                trackPosition.Y,
                Size.X,
                foregroundHeight
            );
        }

        if (foregroundRect.Width > 0 && foregroundRect.Height > 0)
        {
            DrawStyledRectangle(context, foregroundRect, Theme.Foreground);
        }
    }

    protected override Vector2 CalculateGrabberPosition()
    {
        float range = MaxValue - MinValue;
        float normalizedValue = (range == 0) ? 0.0f : (this.Value - MinValue) / range;
        normalizedValue = Math.Clamp(normalizedValue, 0f, 1f);

        if (Direction == VSliderDirection.BottomToTop)
        {
            normalizedValue = 1f - normalizedValue;
        }

        float grabberCenterY = trackPosition.Y + normalizedValue * Size.Y;
        float grabberTopY = grabberCenterY - GrabberSize.Y / 2f;

        grabberTopY = Math.Clamp(grabberTopY, trackPosition.Y, trackPosition.Y + Size.Y - GrabberSize.Y);

        float xPos = trackPosition.X + (Size.X / 2f) - GrabberSize.X / 2f; // Center grabber horizontally
        return new Vector2(xPos, grabberTopY);
    }

    protected override void UpdateGrabberThemeVisuals()
    {
        if (Disabled)
        {
            Theme.Grabber.Current = Theme.Grabber.Disabled;
            return;
        }

        if (grabberPressed)
        {
            Theme.Grabber.Current = Theme.Grabber.Pressed;
        }
        else if (Focused)
        {
            Theme.Grabber.Current = grabberHovered ? Theme.Grabber.Hover : Theme.Grabber.Focused;
        }
        else if (grabberHovered)
        {
            Theme.Grabber.Current = Theme.Grabber.Hover;
        }
        else
        {
            Theme.Grabber.Current = Theme.Grabber.Normal;
        }
    }

    protected override void HandleKeyboardNavigation()
    {
        if (Input.IsActionPressed("UiUp")) // Up arrow typically increases value for vertical controls
        {
            Value = ApplyStep(Value + Step * (Direction == VSliderDirection.TopToBottom ? -1 : 1)); // Inverted logic for TTB
            PlaySound();
        }
        else if (Input.IsActionPressed("UiDown")) // Down arrow decreases value
        {
            Value = ApplyStep(Value - Step * (Direction == VSliderDirection.TopToBottom ? -1 : 1)); // Inverted logic for TTB
            PlaySound();
        }
    }
}