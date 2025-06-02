using Vortice.Mathematics; // For Rect

namespace Cherris;

public class HSlider : Slider
{
    public HSliderDirection Direction { get; set; } = HSliderDirection.LeftToRight;

    protected override void CalculateTrackBounds()
    {
        // trackPosition is the top-left corner of the drawable slider area
        trackPosition = GlobalPosition - Origin;
        trackMin = trackPosition.X;
        trackMax = trackPosition.X + Size.X - GrabberSize.X; // Adjust trackMax to keep grabber fully within bounds
                                                             // Or, allow grabber center to reach extents:
        trackMax = trackPosition.X + Size.X; // Let ConvertPositionToValue and CalculateGrabberPosition handle clamping
    }

    protected override void UpdateHoverStates()
    {
        Vector2 mousePos = GetLocalMousePosition();

        // Track hover: considers the whole Size of the slider
        trackHovered = mousePos.X >= trackPosition.X &&
                       mousePos.X <= trackPosition.X + Size.X &&
                       mousePos.Y >= trackPosition.Y &&
                       mousePos.Y <= trackPosition.Y + Size.Y;

        // Grabber hover: uses actual grabber bounds
        Vector2 grabberCenterPos = CalculateGrabberPosition(); // This is top-left of grabber
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
            if (trackHovered) // If clicked anywhere on the track
            {
                float clampedMouseX = Math.Clamp(GetLocalMousePosition().X, trackPosition.X, trackPosition.X + Size.X);
                Value = ConvertPositionToValue(clampedMouseX);
                grabberPressed = true; // Start "dragging" immediately
                PlaySound();
            }
            // No separate 'else grabberPressed = grabberHovered;' because if trackHovered is true and click, we always start drag.
        }
        else if (Input.IsMouseButtonReleased(MouseButtonCode.Left))
        {
            grabberPressed = false;
        }
    }

    private void HandleMouseDrag()
    {
        if (!grabberPressed) return;

        if (Input.IsMouseButtonReleased(MouseButtonCode.Left)) // Check for release during drag
        {
            grabberPressed = false;
            return;
        }

        float clampedMouseX = Math.Clamp(GetLocalMousePosition().X, trackPosition.X, trackPosition.X + Size.X);
        Value = ConvertPositionToValue(clampedMouseX);
        // PlaySound(); // Optional: play sound on every drag movement, or only on initial click
    }

    private void HandleMouseWheel()
    {
        if (!trackHovered && !grabberHovered) return; // Only respond if mouse is over slider

        float wheelDelta = Input.GetMouseWheelMovement();
        if (wheelDelta == 0) return;

        // Positive wheelDelta usually means scroll up/away, which for HSlider might mean increase value
        Value = ApplyStep(Value + (wheelDelta * Step * (Direction == HSliderDirection.LeftToRight ? 1 : -1)));
        PlaySound();
    }

    protected override float ConvertPositionToValue(float positionOnTrack)
    {
        float effectiveTrackMin = trackPosition.X;
        float effectiveTrackWidth = Size.X;

        if (effectiveTrackWidth <= 0) return MinValue; // Avoid division by zero

        float normalized = (positionOnTrack - effectiveTrackMin) / effectiveTrackWidth;
        normalized = Math.Clamp(normalized, 0f, 1f); // Ensure it's strictly within 0-1

        if (Direction == HSliderDirection.RightToLeft)
        {
            normalized = 1f - normalized;
        }

        float rawValue = MinValue + normalized * (MaxValue - MinValue);
        return ApplyStep(rawValue); // ApplyStep will clamp and quantize
    }

    protected override void DrawForeground(DrawingContext context)
    {
        float range = MaxValue - MinValue;
        float fillRatio = (range == 0) ? 0.0f : (this.Value - MinValue) / range;
        fillRatio = Math.Clamp(fillRatio, 0f, 1f);

        float foregroundWidth = Size.X * fillRatio;
        Rect foregroundRect;

        if (Direction == HSliderDirection.RightToLeft)
        {
            foregroundRect = new Rect(
                trackPosition.X + Size.X - foregroundWidth,
                trackPosition.Y,
                foregroundWidth,
                Size.Y
            );
        }
        else // LeftToRight
        {
            foregroundRect = new Rect(
                trackPosition.X,
                trackPosition.Y,
                foregroundWidth,
                Size.Y
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

        if (Direction == HSliderDirection.RightToLeft)
        {
            normalizedValue = 1f - normalizedValue;
        }

        // Calculate center X of the grabber based on value, then offset by half grabber width
        // Ensure grabber stays within the visual bounds of the track
        float grabberCenterX = trackPosition.X + normalizedValue * Size.X;
        float grabberLeftX = grabberCenterX - GrabberSize.X / 2f;

        // Clamp grabber's left X to ensure it doesn't go outside track area
        grabberLeftX = Math.Clamp(grabberLeftX, trackPosition.X, trackPosition.X + Size.X - GrabberSize.X);

        float yPos = trackPosition.Y + (Size.Y / 2f) - GrabberSize.Y / 2f; // Center grabber vertically
        return new Vector2(grabberLeftX, yPos);
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
        // Assumes Focused is true when called
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