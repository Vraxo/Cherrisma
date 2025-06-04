using Vortice.Mathematics; // For Rect

namespace Cherris;

public class HSlider : Slider
{
    public HSliderDirection Direction { get; set; } = HSliderDirection.LeftToRight;

    protected override void CalculateTrackBounds()
    {
        Vector2 sliderVisualTopLeft = GlobalPosition - Origin;
        this.trackPosition = sliderVisualTopLeft; // Store the visual top-left
        trackMin = sliderVisualTopLeft.X;         // Min X for mouse interaction range, relative to window
        trackMax = sliderVisualTopLeft.X + Size.X;// Max X for mouse interaction range, relative to window
    }

    protected override void UpdateHoverStates()
    {
        Vector2 mousePos = GetLocalMousePosition(); // window-local mouse coordinates
        Vector2 visualSliderTopLeft = this.trackPosition; // This is (GlobalPosition - Origin)

        // Track hover: check if mousePos is within the visual bounds of the slider track
        trackHovered = mousePos.X >= visualSliderTopLeft.X &&
                       mousePos.X <= visualSliderTopLeft.X + Size.X &&
                       mousePos.Y >= visualSliderTopLeft.Y &&
                       mousePos.Y <= visualSliderTopLeft.Y + Size.Y;

        // Grabber hover: CalculateGrabberPosition returns global coords of grabber's top-left
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
            if (trackHovered) // trackHovered uses window-local mouse and this.trackPosition
            {
                float localMouseX = GetLocalMousePosition().X; // Window-local mouse X
                // Value calculation should be based on mouse position relative to the track's visual extent
                // this.trackPosition.X is the visual left edge of the track in window coordinates
                float clampedMouseXOnTrack = Math.Clamp(localMouseX, this.trackPosition.X, this.trackPosition.X + Size.X);
                Value = ConvertPositionToValue(clampedMouseXOnTrack);
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

    protected override float ConvertPositionToValue(float positionOnTrackInWindowSpace)
    {
        float visualTrackLeftEdge = this.trackPosition.X; // This is (GlobalPosition - Origin).X
        float effectiveTrackWidth = Size.X;

        if (effectiveTrackWidth <= 0) return MinValue;

        float normalized = (positionOnTrackInWindowSpace - visualTrackLeftEdge) / effectiveTrackWidth;
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
        Vector2 sliderVisualTopLeft = GlobalPosition - Origin;
        float range = MaxValue - MinValue;
        float fillRatio = (range == 0) ? 0.0f : (this.Value - MinValue) / range;
        fillRatio = Math.Clamp(fillRatio, 0f, 1f);

        float foregroundWidth = Size.X * fillRatio;
        Rect foregroundRect;

        if (Direction == HSliderDirection.RightToLeft)
        {
            foregroundRect = new Rect(
                sliderVisualTopLeft.X + Size.X - foregroundWidth,
                sliderVisualTopLeft.Y,
                foregroundWidth,
                Size.Y
            );
        }
        else // LeftToRight
        {
            foregroundRect = new Rect(
                sliderVisualTopLeft.X,
                sliderVisualTopLeft.Y,
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
        Vector2 sliderVisualTopLeft = GlobalPosition - Origin;
        float range = MaxValue - MinValue;
        float normalizedValue = (range == 0) ? 0.0f : (this.Value - MinValue) / range;
        normalizedValue = Math.Clamp(normalizedValue, 0f, 1f);

        if (Direction == HSliderDirection.RightToLeft)
        {
            normalizedValue = 1f - normalizedValue;
        }

        // Calculate grabber's center X relative to the slider's visual left edge
        float grabberCenterX_relativeToSliderLeft = normalizedValue * Size.X;
        // Calculate grabber's top-left X relative to the slider's visual left edge
        float grabberLeftX_relativeToSliderLeft = grabberCenterX_relativeToSliderLeft - GrabberSize.X / 2f;
        // Clamp this relative position so grabber stays within slider's bounds
        grabberLeftX_relativeToSliderLeft = Math.Clamp(grabberLeftX_relativeToSliderLeft, 0, Size.X - GrabberSize.X);

        // Convert to global coordinates
        float finalGrabberLeftX_global = sliderVisualTopLeft.X + grabberLeftX_relativeToSliderLeft;

        // Calculate grabber's top Y relative to slider's visual top edge (centered vertically)
        float grabberTopY_relativeToSliderTop = (Size.Y / 2f) - GrabberSize.Y / 2f;
        // Convert to global coordinates
        float finalGrabberTopY_global = sliderVisualTopLeft.Y + grabberTopY_relativeToSliderTop;

        return new Vector2(finalGrabberLeftX_global, finalGrabberTopY_global);
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