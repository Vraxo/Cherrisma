namespace Cherris;

[Flags]
public enum AnchorFlags
{
    None = 0,
    AnchorLeft = 1 << 0,
    AnchorTop = 1 << 1,
    AnchorRight = 1 << 2,
    AnchorBottom = 1 << 3,

    CenterHorizontal = 1 << 4, // Center horizontally relative to parent
    CenterVertical = 1 << 5,   // Center vertically relative to parent
    CenterInParent = CenterHorizontal | CenterVertical,

    StretchLeftRight = AnchorLeft | AnchorRight,
    StretchTopBottom = AnchorTop | AnchorBottom,
    StretchFull = StretchLeftRight | StretchTopBottom,
}

public class Node2D : VisualItem
{
    private Vector2 _requestedLocalPosition = Vector2.Zero;
    private Vector2 _explicitlySetGlobalPosition = Vector2.Zero;
    private bool _globalPositionIsExplicitlySet = false;

    public float MarginTop { get; set; } = 0f;
    public float MarginRight { get; set; } = 0f;
    public float MarginBottom { get; set; } = 0f;
    public float MarginLeft { get; set; } = 0f;
    public AnchorFlags Anchors { get; set; } = AnchorFlags.None;

    public virtual Vector2 Position
    {
        get
        {
            if (Parent is not Node2D parentNode || Anchors == AnchorFlags.None)
                return _requestedLocalPosition;

            Vector2 calculatedOriginInParentSpace = Vector2.Zero;
            Vector2 currentSize = this.Size;
            Vector2 currentOriginOffset = this.Origin;

            Vector2 topLeftInParent = Vector2.Zero;

            if (Anchors.HasFlag(AnchorFlags.CenterHorizontal))
            {
                topLeftInParent.X = (parentNode.Size.X - currentSize.X) / 2f;
                topLeftInParent.X += MarginLeft - MarginRight;
                topLeftInParent.X += _requestedLocalPosition.X;
            }
            else if (Anchors.HasFlag(AnchorFlags.AnchorLeft))
            {
                topLeftInParent.X = MarginLeft + _requestedLocalPosition.X;
            }
            else if (Anchors.HasFlag(AnchorFlags.AnchorRight))
            {
                topLeftInParent.X = parentNode.Size.X - currentSize.X - MarginRight + _requestedLocalPosition.X;
            }
            else
            {
                calculatedOriginInParentSpace.X = _requestedLocalPosition.X;
            }

            if (Anchors.HasFlag(AnchorFlags.CenterVertical))
            {
                topLeftInParent.Y = (parentNode.Size.Y - currentSize.Y) / 2f;
                topLeftInParent.Y += MarginTop - MarginBottom;
                topLeftInParent.Y += _requestedLocalPosition.Y;
            }
            else if (Anchors.HasFlag(AnchorFlags.AnchorTop))
            {
                topLeftInParent.Y = MarginTop + _requestedLocalPosition.Y;
            }
            else if (Anchors.HasFlag(AnchorFlags.AnchorBottom))
            {
                topLeftInParent.Y = parentNode.Size.Y - currentSize.Y - MarginBottom + _requestedLocalPosition.Y;
            }
            else
            {
                calculatedOriginInParentSpace.Y = _requestedLocalPosition.Y;
            }

            bool anchoredX = Anchors.HasFlag(AnchorFlags.AnchorLeft) || Anchors.HasFlag(AnchorFlags.AnchorRight) || Anchors.HasFlag(AnchorFlags.CenterHorizontal);
            bool anchoredY = Anchors.HasFlag(AnchorFlags.AnchorTop) || Anchors.HasFlag(AnchorFlags.AnchorBottom) || Anchors.HasFlag(AnchorFlags.CenterVertical);

            if (anchoredX)
                calculatedOriginInParentSpace.X = topLeftInParent.X + currentOriginOffset.X;
            if (anchoredY)
                calculatedOriginInParentSpace.Y = topLeftInParent.Y + currentOriginOffset.Y;

            return calculatedOriginInParentSpace;
        }
        set
        {
            _requestedLocalPosition = value;
            _globalPositionIsExplicitlySet = false;
        }
    }

    public virtual Vector2 GlobalPosition
    {
        get
        {
            if (_globalPositionIsExplicitlySet || !InheritPosition)
                return _explicitlySetGlobalPosition;

            if (Parent is Node2D parentNode)
            {
                return parentNode.GlobalPosition + Position;
            }
            return Position;
        }
        set
        {
            _explicitlySetGlobalPosition = value;
            _globalPositionIsExplicitlySet = true;
            if (Parent is not Node2D)
            {
                _requestedLocalPosition = value;
            }
        }
    }

    public override Vector2 Size
    {
        get
        {
            Vector2 baseSize = base.Size;
            if (Parent is not Node2D parentNode || Anchors == AnchorFlags.None)
                return baseSize;

            bool stretchX = Anchors.HasFlag(AnchorFlags.AnchorLeft) && Anchors.HasFlag(AnchorFlags.AnchorRight);
            bool stretchY = Anchors.HasFlag(AnchorFlags.AnchorTop) && Anchors.HasFlag(AnchorFlags.AnchorBottom);

            if (!stretchX && !stretchY)
                return baseSize;

            float finalWidth = stretchX ? Math.Max(0, parentNode.Size.X - MarginLeft - MarginRight) : baseSize.X;
            float finalHeight = stretchY ? Math.Max(0, parentNode.Size.Y - MarginTop - MarginBottom) : baseSize.Y;

            return new Vector2(finalWidth, finalHeight);
        }
        set => base.Size = value;
    }

    public virtual float Rotation { get; set; } = 0;
    public OriginPreset OriginPreset { get; set; } = OriginPreset.Center;
    public bool InheritPosition { get; set; } = true;
    public bool InheritOrigin { get; set; } = false;
    public bool InheritScale { get; set; } = true;
    public HAlignment HAlignment { get; set; } = HAlignment.Center;
    public VAlignment VAlignment { get; set; } = VAlignment.Center;

    public Vector2 ScaledSize => Size * Scale;

    private Vector2 _scale = Vector2.One;
    public virtual Vector2 Scale
    {
        get => InheritScale && Parent is Node2D node2DParent ? node2DParent.Scale : _scale;
        set => _scale = value;
    }

    private Vector2 _offset = Vector2.Zero;
    public Vector2 Offset
    {
        get => InheritOrigin && Parent is Node2D parentNode ? parentNode.Offset : _offset;
        set => _offset = value;
    }

    public Vector2 Origin
    {
        get
        {
            Vector2 currentSize = this.Size;
            Vector2 currentScale = this.Scale;

            float x = OriginPreset switch
            {
                OriginPreset.TopLeft or OriginPreset.CenterLeft or OriginPreset.BottomLeft => 0,
                OriginPreset.TopCenter or OriginPreset.Center or OriginPreset.BottomCenter => currentSize.X * currentScale.X / 2,
                OriginPreset.TopRight or OriginPreset.CenterRight or OriginPreset.BottomRight => currentSize.X * currentScale.X,
                _ => HAlignment switch
                {
                    HAlignment.Center => currentSize.X * currentScale.X / 2,
                    HAlignment.Left => 0,
                    HAlignment.Right => currentSize.X * currentScale.X,
                    _ => 0
                }
            };

            float y = OriginPreset switch
            {
                OriginPreset.TopLeft or OriginPreset.TopCenter or OriginPreset.TopRight => 0,
                OriginPreset.CenterLeft or OriginPreset.Center or OriginPreset.CenterRight => currentSize.Y * currentScale.Y / 2,
                OriginPreset.BottomLeft or OriginPreset.BottomCenter or OriginPreset.BottomRight => currentSize.Y * currentScale.Y,
                _ => VAlignment switch
                {
                    VAlignment.Top => 0,
                    VAlignment.Center => currentSize.Y * currentScale.Y / 2,
                    VAlignment.Bottom => currentSize.Y * currentScale.Y,
                    _ => 0
                }
            };

            return new Vector2(x, y) + Offset;
        }
    }

    public void LookAt(Vector2 targetPosition)
    {
        Vector2 direction = targetPosition - GlobalPosition;
        var angle = float.Atan2(direction.Y, direction.X) * 57.29578f;
        Rotation = angle;
    }
}