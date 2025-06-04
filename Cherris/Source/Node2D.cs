namespace Cherris;

public class Node2D : VisualItem
{
    public Vector2 Position { get; set; } = Vector2.Zero;
    public virtual float Rotation { get; set; } = 0;
    public OriginPreset OriginPreset { get; set; } = OriginPreset.Center; // Note: OriginPreset is currently not used by Origin getter.
    public bool InheritScale { get; set; } = true;
    public HAlignment HAlignment { get; set; } = HAlignment.Center;
    public VAlignment VAlignment { get; set; } = VAlignment.Center;
    public AnchorPreset AnchorPreset { get; set; } = AnchorPreset.None;

    public float MarginLeft { get; set; } = 0f;
    public float MarginTop { get; set; } = 0f;
    public float MarginRight { get; set; } = 0f;
    public float MarginBottom { get; set; } = 0f;

    public float RelativeWidth { get; set; } = 0f; // 0 to 1. If > 0, overrides explicit width.
    public float RelativeHeight { get; set; } = 0f; // 0 to 1. If > 0, overrides explicit height.

    public Vector2 ScaledSize => Size * Scale;

    public virtual Vector2 Size
    {
        get
        {
            Vector2 referenceSizeForRelative;

            if (Parent is Node2D parentNode2D)
            {
                referenceSizeForRelative = parentNode2D.Size;
            }
            else
            {
                referenceSizeForRelative = GetWindowSizeV2();
            }

            float finalWidth = _explicitSize.X;
            if (RelativeWidth > 0f && RelativeWidth <= 1f)
            {
                finalWidth = referenceSizeForRelative.X * RelativeWidth;
            }

            float finalHeight = _explicitSize.Y;
            if (RelativeHeight > 0f && RelativeHeight <= 1f)
            {
                finalHeight = referenceSizeForRelative.Y * RelativeHeight;
            }

            return new Vector2(finalWidth, finalHeight);
        }
        set
        {
            if (_explicitSize == value) return;
            _explicitSize = value;
            SizeChanged?.Invoke(this, Size);
        }
    }

    public virtual Vector2 Scale
    {
        get => InheritScale && Parent is Node2D node2DParent ? node2DParent.Scale : field;
        set => field = value;
    } = new(1, 1);

    public virtual Vector2 GlobalPosition
    {
        get
        {
            Vector2 parentGlobalTopLeft; // Top-left of the parent's bounding box in global space
            Vector2 parentSize;

            if (Parent is Node2D parentNode)
            {
                // To get parent's global top-left, we need its GlobalPosition (which is its Origin) and its Origin
                parentGlobalTopLeft = parentNode.GlobalPosition - parentNode.Origin;
                parentSize = parentNode.Size;
            }
            else
            {
                parentGlobalTopLeft = Vector2.Zero; // Root relative to window
                parentSize = GetWindowSizeV2();
            }

            float calculatedGlobalOriginX;
            float calculatedGlobalOriginY;

            if (AnchorPreset == AnchorPreset.None)
            {
                // If no anchor, GlobalPosition is parent's top-left + local Position
                // (semantically, local Position is relative to parent's top-left if parent is root,
                // or parent's Origin if parent is Node2D - for consistency, let's use parent's top-left)
                // No, GlobalPosition of a child is relative to parent's origin for direct children
                Vector2 parentOriginGlobal = (Parent is Node2D pNode) ? pNode.GlobalPosition : Vector2.Zero;
                calculatedGlobalOriginX = parentOriginGlobal.X + Position.X;
                calculatedGlobalOriginY = parentOriginGlobal.Y + Position.Y;
            }
            else
            {
                // Calculate the target anchor point on the parent in global space
                float targetGlobalAnchorX = AnchorPreset switch
                {
                    AnchorPreset.TopLeft or AnchorPreset.CenterLeft or AnchorPreset.BottomLeft
                        => parentGlobalTopLeft.X + MarginLeft,
                    AnchorPreset.TopCenter or AnchorPreset.Center or AnchorPreset.BottomCenter
                        => parentGlobalTopLeft.X + (parentSize.X * 0.5f) + MarginLeft - MarginRight,
                    _ // TopRight, CenterRight, BottomRight
                        => parentGlobalTopLeft.X + parentSize.X - MarginRight,
                };

                float targetGlobalAnchorY = AnchorPreset switch
                {
                    AnchorPreset.TopLeft or AnchorPreset.TopCenter or AnchorPreset.TopRight
                        => parentGlobalTopLeft.Y + MarginTop,
                    AnchorPreset.CenterLeft or AnchorPreset.Center or AnchorPreset.CenterRight
                        => parentGlobalTopLeft.Y + (parentSize.Y * 0.5f) + MarginTop - MarginBottom,
                    _ // BottomLeft, BottomCenter, BottomRight
                        => parentGlobalTopLeft.Y + parentSize.Y - MarginBottom,
                };

                // The node's own Origin should be placed at this target global anchor point.
                // So, GlobalPosition.X (which is the origin's X) IS targetGlobalAnchorX.
                calculatedGlobalOriginX = targetGlobalAnchorX;
                calculatedGlobalOriginY = targetGlobalAnchorY;

                // Then, the node's local `Position` property acts as an additional offset
                // from this already-calculated anchored origin position.
                calculatedGlobalOriginX += Position.X;
                calculatedGlobalOriginY += Position.Y;
            }

            return new(calculatedGlobalOriginX, calculatedGlobalOriginY);
        }
    }

    public Vector2 Offset { get; set; } // Offset applied to Origin calculation

    public Vector2 Origin // This is the node's pivot point, relative to its own top-left corner
    {
        get
        {
            // Note: OriginPreset is currently not used here.
            // HAlignment/VAlignment directly determine the origin.
            float x = HAlignment switch
            {
                HAlignment.Center => Size.X / 2f,
                HAlignment.Left => 0,
                HAlignment.Right => Size.X,
                HAlignment.None => 0, // Default to Left if None
                _ => 0
            };

            float y = VAlignment switch
            {
                VAlignment.Center => Size.Y / 2f,
                VAlignment.Top => 0,
                VAlignment.Bottom => Size.Y,
                VAlignment.None => 0, // Default to Top if None
                _ => 0
            };

            Vector2 alignmentOffset = new(x, y);
            return alignmentOffset + Offset; // Apply additional Offset
        }
    }

    protected Vector2 _explicitSize = new(320, 320);
    private Vector2 fieldScale = new(1, 1); // Backing field for Scale if not inheriting

    public event EventHandler<Vector2>? SizeChanged;

    public void LookAt(Vector2 targetPosition)
    {
        Vector2 originPoint = GlobalPosition; // GlobalPosition is already the origin's position
        Vector2 direction = targetPosition - originPoint;
        var angle = float.Atan2(direction.Y, direction.X) * 57.29578f; // Radians to Degrees
        Rotation = angle;
    }
}