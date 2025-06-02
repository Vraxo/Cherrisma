using Vortice.Mathematics; // For Rect

namespace Cherris;

public class HSlider : Slider
{
    public HSliderDirection Direction { get; set; } = HSliderDirection.LeftToRight;

    protected override void CalculateTrackBounds()
    {
        // trackPosition is the top-left corner of the drawable slider area.
        // Since GlobalPosition is now the top-left of the Node2D's bounding box,
        // trackPosition can be set directly to GlobalPosition.
        trackPosition = GlobalPosition;

        // trackMin and trackMax are used for converting mouse position to slider value.
        // They should represent the clickable/effective range of the track itself.
        trackMin = trackPosition.X;
        // If the grabber's center can align with the track ends:
        trackMax = trackPosition.X + Size.X;
        // If the grabber must stay fully within track visually (for mouse interaction):
        // trackMax = trackPosition.X + Size.X - GrabberSize.X; (This might be too restrictive for value calculation)
        // Let ConvertPositionToValue and CalculateGrabberPosition handle clamping based on full Size.X range.
    }

    protected override void UpdateHoverStates()
    {
        Vector2 mousePos = GetLocalMousePosition();

        // Track hover: considers the whole Size of the slider, relative to its top-left (trackPosition)
        trackHovered = mousePos.X >= trackPosition.X &&
                       mousePos.X <= trackPosition.X + Size.X &&
                       mousePos.Y >= trackPosition.Y &&
                       mousePos.Y <= trackPosition.Y + Size.Y;

        // Grabber hover: uses actual grabber bounds
        Vector2 grabberTopLeftPos = CalculateGrabberPosition(); // This is top-left of grabber
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
            // We need to check if it's within the slider's bounds (trackPosition to trackPosition + Size).
            float localMouseX = GetLocalMousePosition().X;

            if (trackHovered) // If clicked anywhere on the track (trackHovered already checks this)
            {
                // Clamp mouse X to be within the visual track extents for value calculation
                float clampedMouseXOnTrack = Math.Clamp(localMouseX, trackPosition.X, trackPosition.X + Size.X);
                Value = ConvertPositionToValue(clampedMouseXOnTrack);
                grabberPressed = true; // Start "dragging" immediately
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

        if (Input.IsMouseButtonReleased(MouseButtonCode.Left)) // Check for release during drag
        {
            grabberPressed = false;
            return;
        }

        float localMouseX = GetLocalMousePosition().X;
        // Clamp mouse X to be within the visual track extents for value calculation
        float clampedMouseXOnTrack = Math.Clamp(localMouseX, trackPosition.X, trackPosition.X + Size.X);
        Value = ConvertPositionToValue(clampedMouseXOnTrack);
    }

    private void HandleMouseWheel()
    {
        if (!trackHovered && !grabberHovered) return; // Only respond if mouse is over slider

        float wheelDelta = Input.GetMouseWheelMovement();
        if (wheelDelta == 0) return;

        Value = ApplyStep(Value + (wheelDelta * Step * (Direction == HSliderDirection.LeftToRight ? 1 : -1)));
        PlaySound();
    }

    protected override float ConvertPositionToValue(float positionOnTrackInLocalSpace)
    {
        // positionOnTrackInLocalSpace is a local X coordinate (e.g., mouse.X)
        // effectiveTrackMin is the track's starting X coordinate (trackPosition.X)
        float effectiveTrackMin = trackPosition.X;
        float effectiveTrackWidth = Size.X;

        if (effectiveTrackWidth <= 0) return MinValue;

        // Normalize the position relative to the track's start and width
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
        float range = MaxValue - MinValue;
        float fillRatio = (range == 0) ? 0.0f : (this.Value - MinValue) / range;
        fillRatio = Math.Clamp(fillRatio, 0f, 1f);

        float foregroundWidth = Size.X * fillRatio;
        Rect foregroundRect;

        // trackPosition is the top-left of the slider.
        if (Direction == HSliderDirection.RightToLeft)
        {
            foregroundRect = new Rect(
                trackPosition.X + Size.X - foregroundWidth, // Starts from the right and extends left
                trackPosition.Y,
                foregroundWidth,
                Size.Y
            );
        }
        else // LeftToRight
        {
            foregroundRect = new Rect(
                trackPosition.X, // Starts from the left
                trackPosition.Y,
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
        // Returns the TOP-LEFT position of the grabber.
        float range = MaxValue - MinValue;
        float normalizedValue = (range == 0) ? 0.0f : (this.Value - MinValue) / range;
        normalizedValue = Math.Clamp(normalizedValue, 0f, 1f);

        if (Direction == HSliderDirection.RightToLeft)
        {
            normalizedValue = 1f - normalizedValue;
        }

        // Calculate the CENTER X of where the grabber should be based on value.
        float grabberCenterX = trackPosition.X + normalizedValue * Size.X;

        // Calculate the TOP-LEFT X of the grabber.
        float grabberLeftX = grabberCenterX - GrabberSize.X / 2f;

        // Clamp grabber's top-left X to ensure it doesn't go visually outside the main track area
        // The grabber should appear to slide along the track.
        grabberLeftX = Math.Clamp(grabberLeftX, trackPosition.X, trackPosition.X + Size.X - GrabberSize.X);

        float yPos = trackPosition.Y + (Size.Y / 2f) - GrabberSize.Y / 2f; // Center grabber vertically within the track
        return new Vector2(grabberLeftX, yPos);
    }

    protected override void UpdateGrabberThemeVisuals()
    {
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
            // If focused, use Hover style if also hovered, otherwise Focused style.
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