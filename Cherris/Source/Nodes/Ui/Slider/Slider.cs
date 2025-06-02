using Vortice.Mathematics; // For Rect

namespace Cherris;

public abstract class Slider : Control
{
    // Add this property
    public bool SuppressValueChangedEvent { get; set; } = false;

    private float _value = 0.5f;
    public float Value
    {
        get => _value;
        set
        {
            float newValue = ApplyStep(value); // ApplyStep also clamps
            
            if (_value == newValue)
            {
                return;
            }

            _value = newValue;
            
            if (SuppressValueChangedEvent) // Check the flag
            {
                return;
            }

            ValueChanged?.Invoke(_value); // Invoke the event
        }
    }

    public float MinValue { get; set; } = 0f;
    public float MaxValue { get; set; } = 1f;
    public Sound? MoveSound { get; set; }
    public SliderStyle Style { get; set; } = new();
    public Vector2 GrabberSize { get; set; } = new(12, 24);

    protected bool grabberPressed;
    protected bool grabberHovered;
    protected bool trackHovered;
    protected Vector2 trackPosition;
    protected float trackMin;
    protected float trackMax;

    private float _step = 0.01f;
    public float Step
    {
        get => _step;
        set => _step = Math.Max(value, 0f);
    }

    public event Action<float>? ValueChanged; // Added event

    public Slider()
    {
        Size = new(200, 16);
        Focusable = true;
        Navigable = true;

        Style.Foreground.FillColor = DefaultTheme.Accent;
        Style.Foreground.Roundness = 0.2f;
        Style.Background.Roundness = 0.2f;
        Style.Grabber.Roundness = 0.5f;
        Style.Grabber.BorderLength = 1f;
        Style.Grabber.Normal.FillColor = DefaultTheme.NormalFill;
        Style.Grabber.Hover.FillColor = DefaultTheme.HoverFill;
        Style.Grabber.Pressed.FillColor = DefaultTheme.Accent;
        Style.Grabber.Focused.BorderColor = DefaultTheme.FocusBorder;
    }

    public override void Process()
    {
        base.Process();

        if (Disabled)
        {
            grabberPressed = false;
            UpdateGrabberThemeVisuals();
            return;
        }

        CalculateTrackBounds();
        UpdateHoverStates();

        if (Input.IsMouseButtonPressed(MouseButtonCode.Left))
        {
            HandleClickFocus();
        }

        HandleInput();

        if (Focused)
        {
            HandleKeyboardNavigation();
            OnFocusLost();
        }

        UpdateGrabberThemeVisuals();
    }

    protected Vector2 GetLocalMousePosition()
    {
        var owningWindowNode = GetOwningWindowNode();
        return owningWindowNode?.LocalMousePosition ?? Input.MousePosition;
    }

    protected abstract void HandleKeyboardNavigation();

    protected virtual void OnFocusLost()
    {
        if (Input.IsActionPressed("UiAccept") ||
            (Input.IsMouseButtonPressed(MouseButtonCode.Left) && !IsMouseOver()))
        {
            Focused = false;
        }
    }

    protected override void OnThemeFileChanged(string themeFile)
    {
        try
        {
            Style = FileLoader.Load<SliderStyle>(themeFile);
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to load SliderStyle from '{themeFile}': {ex.Message}");
        }
    }

    protected void PlaySound()
    {
        if (!Disabled)
        {
            MoveSound?.Play(AudioBus);
        }
    }

    protected abstract void CalculateTrackBounds();
    protected abstract void UpdateHoverStates();
    protected abstract void HandleInput();
    protected abstract void UpdateGrabberThemeVisuals();
    protected abstract float ConvertPositionToValue(float position);
    protected abstract void DrawForeground(DrawingContext context);
    protected abstract Vector2 CalculateGrabberPosition();

    protected float ApplyStep(float rawValue)
    {
        float clampedValue = Math.Clamp(rawValue, MinValue, MaxValue);

        if (Step <= 0 || MinValue >= MaxValue)
        {
            return clampedValue;
        }

        float steppedValue = MinValue + (float)Math.Round((clampedValue - MinValue) / Step) * Step;
        return Math.Clamp(steppedValue, MinValue, MaxValue);
    }

    public override void Draw(DrawingContext context)
    {
        if (!Visible) return;

        DrawBackground(context);
        DrawForeground(context);
        DrawGrabber(context);
    }

    private void DrawBackground(DrawingContext context)
    {
        var bounds = new Rect(trackPosition.X, trackPosition.Y, Size.X, Size.Y);
        DrawStyledRectangle(context, bounds, Style.Background);
    }

    private void DrawGrabber(DrawingContext context)
    {
        Vector2 grabberPos = CalculateGrabberPosition();
        ButtonStyle currentGrabberStyle = Style.Grabber.Current;

        var bounds = new Rect(grabberPos.X, grabberPos.Y, GrabberSize.X, GrabberSize.Y);
        DrawStyledRectangle(context, bounds, currentGrabberStyle);
    }

    protected override void HandleClickFocus()
    {
        if (Disabled || !Focusable) return;

        if (trackHovered || grabberHovered)
        {
            Focused = true;
        }
    }
}