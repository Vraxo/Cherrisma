namespace Cherris;

public class SliderStyle
{
    public BoxStyle Background { get; set; } = new()
    {
        FillColor = DefaultTheme.DisabledFill, // Example default
        BorderColor = DefaultTheme.DisabledBorder
    };
    public BoxStyle Foreground { get; set; } = new()
    {
        FillColor = DefaultTheme.Accent, // Example default
        BorderColor = DefaultTheme.AccentBorder
    };
    public ButtonStylePack Grabber { get; set; } = new();
}