namespace Cherris;

public enum OriginPreset
{
    None,
    Center,
    CenterLeft,
    CenterRight,
    TopLeft,
    TopCenter,
    TopRight,
    BottomCenter,
    BottomLeft,
    BottomRight,
}

public enum HAlignment
{
    Left,
    Center,
    Right,
    None
}

public enum LayoutAnchorPreset
{
    None, // TopLeft by default if margins are sizes

    // Corners
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,

    // Sides
    CenterLeft,
    CenterRight,
    TopCenter,
    BottomCenter,

    // Center
    Center,

    // Spanning
    LeftWide,   // Full height, anchored to left
    RightWide,  // Full height, anchored to right
    TopWide,    // Full width, anchored to top
    BottomWide, // Full width, anchored to bottom

    // Centered Spanning
    VCenterWide, // Full width, vertically centered
    HCenterWide, // Full height, horizontally centered

    // Full
    FullRect
}