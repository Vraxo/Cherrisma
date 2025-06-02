namespace Cherris;

public class Node2D : VisualItem
{
    public Vector2 Position          { get; set; } = Vector2.Zero;
    public virtual float Rotation    { get; set; } = 0;
    public OriginPreset OriginPreset { get; set; } = OriginPreset.Center;
    public bool InheritScale         { get; set; } = true;
    public HAlignment HAlignment     { get; set; } = HAlignment.Center;
    public VAlignment VAlignment     { get; set; } = VAlignment.Center;
    public AnchorPreset AnchorPreset { get; set; } = AnchorPreset.None;

    public float MarginLeft   { get; set; } = 0f;
    public float MarginTop    { get; set; } = 0f;
    public float MarginRight  { get; set; } = 0f;
    public float MarginBottom { get; set; } = 0f;

    public float RelativeWidth  { get; set; } = 0f; // 0 to 1. If > 0, overrides explicit width.
    public float RelativeHeight { get; set; } = 0f; // 0 to 1. If > 0, overrides explicit height.

    public Vector2 ScaledSize => Size * Scale;

    public virtual Vector2 Size
    {
        get
        {
            Vector2 referenceSizeForRelative;

            if (Parent is Node2D parentNode2D)
            {
                referenceSizeForRelative = parentNode2D.Size; // Parent's calculated size
            }
            else
            {
                referenceSizeForRelative = GetWindowSizeV2(); // Window size if no Node2D parent
            }

            float finalWidth = _explicitSize.X;
            // If RelativeWidth is set (e.g. 0.5 for 50%), calculate width based on reference.
            if (RelativeWidth > 0f && RelativeWidth <= 1f)
            {
                finalWidth = referenceSizeForRelative.X * RelativeWidth;
            }

            float finalHeight = _explicitSize.Y;
            // If RelativeHeight is set, calculate height based on reference.
            if (RelativeHeight > 0f && RelativeHeight <= 1f)
            {
                finalHeight = referenceSizeForRelative.Y * RelativeHeight;
            }

            return new Vector2(finalWidth, finalHeight);
        }
        set // This setter updates the _explicitSize. Relative sizing remains active if configured.
        {
            if (_explicitSize == value)
            {
                return;
            }

            _explicitSize = value;
            // Invoke SizeChanged with the new *calculated* size, as the explicit base has changed.
            SizeChanged?.Invoke(this, Size);
        }
    }

    public virtual Vector2 Scale
    {
        get
        {
            return InheritScale && Parent is Node2D node2DParent
                ? node2DParent.Scale
                : field;
        }

        set;
    }

    public virtual Vector2 GlobalPosition
    {
        get
        {
            Vector2 parentTopLeft;
            Vector2 parentSize;

            if (Parent is Node2D parentNode)
            {
                parentTopLeft = parentNode.GlobalPosition;
                parentSize = parentNode.Size;
            }
            else
            {
                parentTopLeft = Vector2.Zero;
                parentSize = GetWindowSizeV2();
            }

            float myCalculatedTopLeftX;
            float myCalculatedTopLeftY;

            if (AnchorPreset == AnchorPreset.None)
            {
                myCalculatedTopLeftX = parentTopLeft.X + Position.X;
                myCalculatedTopLeftY = parentTopLeft.Y + Position.Y;
            }
            else
            {
                myCalculatedTopLeftX = AnchorPreset switch
                {
                    AnchorPreset.TopLeft or AnchorPreset.CenterLeft or AnchorPreset.BottomLeft => parentTopLeft.X + MarginLeft,
                    AnchorPreset.TopCenter or AnchorPreset.Center or AnchorPreset.BottomCenter => parentTopLeft.X + (parentSize.X * 0.5f) - (Size.X * 0.5f) + MarginLeft,
                    _ => parentTopLeft.X + parentSize.X - Size.X - MarginRight,
                };

                myCalculatedTopLeftY = AnchorPreset switch
                {
                    AnchorPreset.TopLeft or AnchorPreset.TopCenter or AnchorPreset.TopRight => parentTopLeft.Y + MarginTop,
                    AnchorPreset.CenterLeft or AnchorPreset.Center or AnchorPreset.CenterRight => parentTopLeft.Y + (parentSize.Y * 0.5f) - (Size.Y * 0.5f) + MarginTop,
                    _ => parentTopLeft.Y + parentSize.Y - Size.Y - MarginBottom,
                };

                myCalculatedTopLeftX += Position.X;
                myCalculatedTopLeftY += Position.Y;
            }

            return new(myCalculatedTopLeftX, myCalculatedTopLeftY);
        }
    }

    public Vector2 Offset { get; set;}

    public Vector2 Origin
    {
        get
        {
            float x = HAlignment switch
            {
                HAlignment.Center => Size.X / 2f,
                HAlignment.Left => 0,
                HAlignment.Right => Size.X,
                _ => 0
            };

            float y = VAlignment switch
            {
                VAlignment.Top => 0,
                VAlignment.Center => Size.Y / 2f,
                VAlignment.Bottom => Size.Y,
                _ => 0
            };

            Vector2 alignmentOffset = new(x, y);
            return alignmentOffset + Offset;
        }
    }

    private Vector2 _explicitSize = new(320, 320);

    // Events

    public event EventHandler<Vector2>? SizeChanged;

    // API

    public void LookAt(Vector2 targetPosition)
    {
        Vector2 originPoint = GlobalPosition + Origin;
        Vector2 direction = targetPosition - originPoint;
        var angle = float.Atan2(direction.Y, direction.X) * 57.29578f;
        Rotation = angle;
    }
}