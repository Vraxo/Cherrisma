namespace Cherris;

public class Control : Node2D
{
    private float _anchorLeft = 0f;
    private float _anchorTop = 0f;
    private float _anchorRight = 0f;
    private float _anchorBottom = 0f;

    private float _marginLeft = 0f;
    private float _marginTop = 0f;
    private float _marginRight = 0f; // Can mean width or offset depending on anchors
    private float _marginBottom = 0f; // Can mean height or offset depending on anchors

    private LayoutAnchorPreset _currentLayoutAnchorPreset = LayoutAnchorPreset.None;

    // Store the position and size that were last calculated by UpdateLayout
    private Vector2 _calculatedPosition = Vector2.Zero;
    private Vector2 _calculatedSize = Vector2.Zero;

    public float AnchorLeft { get => _anchorLeft; set { if (_anchorLeft == value) return; _anchorLeft = value; RequestLayoutUpdate(); } }
    public float AnchorTop { get => _anchorTop; set { if (_anchorTop == value) return; _anchorTop = value; RequestLayoutUpdate(); } }
    public float AnchorRight { get => _anchorRight; set { if (_anchorRight == value) return; _anchorRight = value; RequestLayoutUpdate(); } }
    public float AnchorBottom { get => _anchorBottom; set { if (_anchorBottom == value) return; _anchorBottom = value; RequestLayoutUpdate(); } }

    public float MarginLeft { get => _marginLeft; set { if (_marginLeft == value) return; _marginLeft = value; RequestLayoutUpdate(); } }
    public float MarginTop { get => _marginTop; set { if (_marginTop == value) return; _marginTop = value; RequestLayoutUpdate(); } }
    public float MarginRight { get => _marginRight; set { if (_marginRight == value) return; _marginRight = value; RequestLayoutUpdate(); } }
    public float MarginBottom { get => _marginBottom; set { if (_marginBottom == value) return; _marginBottom = value; RequestLayoutUpdate(); } }

    private bool _layoutUpdateRequested = true;

    public LayoutAnchorPreset CurrentLayoutAnchorPreset
    {
        get => _currentLayoutAnchorPreset;
        set
        {
            if (_currentLayoutAnchorPreset == value) return;
            _currentLayoutAnchorPreset = value;
            ApplyAnchorPreset(value);
            RequestLayoutUpdate();
        }
    }

    public override Vector2 Position
    {
        get
        {
            if (_layoutUpdateRequested) UpdateLayout(); // Ensure layout is up-to-date before returning
            return base.Position; // Which is _calculatedPosition set by UpdateLayout
        }
        set
        {
            // When user sets Position directly, adjust margins to achieve this position based on current anchors
            Vector2 parentSize = GetParentSize();
            MarginLeft = value.X - (parentSize.X * AnchorLeft);
            MarginTop = value.Y - (parentSize.Y * AnchorTop);
            // Note: This doesn't change AnchorRight/Bottom margins. If control is sized by anchors, this might be unexpected.
            // For controls sized by anchors, setting position might not make sense without also adjusting size or other margins.
            RequestLayoutUpdate();
        }
    }

    public override Vector2 Size
    {
        get
        {
            if (_layoutUpdateRequested) UpdateLayout(); // Ensure layout is up-to-date
            return base.Size; // Which is _calculatedSize set by UpdateLayout
        }
        set
        {
            // When user sets Size directly, adjust MarginRight and MarginBottom
            Vector2 parentSize = GetParentSize();
            Vector2 currentP = base.Position; // Use the already calculated/set position

            // If anchors define width (AnchorLeft < AnchorRight), then setting Size.X adjusts MarginRight
            if (AnchorLeft < AnchorRight)
            {
                MarginRight = (currentP.X + value.X) - (parentSize.X * AnchorRight);
            }
            else // Anchors don't define width, MarginRight is treated as width itself
            {
                MarginRight = value.X;
            }

            // If anchors define height (AnchorTop < AnchorBottom), then setting Size.Y adjusts MarginBottom
            if (AnchorTop < AnchorBottom)
            {
                MarginBottom = (currentP.Y + value.Y) - (parentSize.Y * AnchorBottom);
            }
            else // Anchors don't define height, MarginBottom is treated as height itself
            {
                MarginBottom = value.Y;
            }
            RequestLayoutUpdate();
        }
    }


    public Control()
    {
        // Default size for a control if not specified by margins/anchors
        _marginRight = 100f; // Default width
        _marginBottom = 100f; // Default height
        _calculatedSize = new Vector2(_marginRight, _marginBottom);
        base.Size = _calculatedSize; // Initialize base size
    }

    public override void Process()
    {
        base.Process();
        if (_layoutUpdateRequested)
        {
            UpdateLayout();
        }
    }

    protected void RequestLayoutUpdate()
    {
        _layoutUpdateRequested = true;
    }

    protected virtual void UpdateLayout()
    {
        if (!_layoutUpdateRequested && !IsTransformOrParentSizeDirty()) return;

        Vector2 parentSize = GetParentSize();
        Vector2 newPosition = Vector2.Zero;
        Vector2 newSize = Vector2.Zero;

        // Calculate Top-Left corner
        newPosition.X = parentSize.X * AnchorLeft + MarginLeft;
        newPosition.Y = parentSize.Y * AnchorTop + MarginTop;

        // Calculate Size
        // If AnchorRight is not set to expand beyond AnchorLeft, MarginRight is treated as width
        if (AnchorRight <= AnchorLeft) // Or Math.Abs(AnchorRight - AnchorLeft) < float.Epsilon
        {
            newSize.X = MarginRight;
        }
        else // Anchors define width: right_anchor_pos + margin_right_offset - left_pos
        {
            newSize.X = (parentSize.X * AnchorRight + MarginRight) - newPosition.X;
        }

        // If AnchorBottom is not set to expand beyond AnchorTop, MarginBottom is treated as height
        if (AnchorBottom <= AnchorTop) // Or Math.Abs(AnchorBottom - AnchorTop) < float.Epsilon
        {
            newSize.Y = MarginBottom;
        }
        else // Anchors define height: bottom_anchor_pos + margin_bottom_offset - top_pos
        {
            newSize.Y = (parentSize.Y * AnchorBottom + MarginBottom) - newPosition.Y;
        }
        
        newSize.X = Math.Max(0, newSize.X); // Ensure non-negative size
        newSize.Y = Math.Max(0, newSize.Y);

        // Check if position or size actually changed to avoid unnecessary updates
        bool positionChanged = base.Position != newPosition;
        bool sizeChanged = base.Size != newSize;

        if (positionChanged)
        {
            base.Position = newPosition;
        }
        if (sizeChanged)
        {
            base.Size = newSize; // This will trigger Node2D's SizeChanged event if overridden correctly
        }
        
        _calculatedPosition = newPosition;
        _calculatedSize = newSize;

        _layoutUpdateRequested = false;
        _lastParentSize = parentSize; // Cache parent size for dirty checking
    }
    
    private Vector2 _lastParentSize = new Vector2(-1,-1); // Initialize to ensure first check is dirty

    private bool IsTransformOrParentSizeDirty()
    {
        // This is a simplified dirty check. A more robust system might involve events.
        Vector2 currentParentSize = GetParentSize();
        if (currentParentSize != _lastParentSize)
        {
            return true;
        }
        // Could add checks for own anchor/margin properties if they could be changed externally
        // without calling RequestLayoutUpdate(), but property setters already do that.
        return false;
    }


    protected Vector2 GetParentSize()
    {
        if (Parent is Control parentControl)
        {
            return parentControl.Size; // Use the calculated size of the parent Control
        }
        if (Parent is Node2D parentNode2D)
        {
            // If parent is Node2D but not Control, it might not have UI-centric size.
            // Fallback to window or use its explicit Size. Using explicit Size for now.
            return parentNode2D.Size;
        }
        // If no suitable parent, use the owning window's size
        return GetWindowSizeV2();
    }

    protected void ApplyAnchorPreset(LayoutAnchorPreset preset)
    {
        // Store current margins that might represent size
        float currentWidth = (AnchorRight <= AnchorLeft) ? MarginRight : 0;
        float currentHeight = (AnchorBottom <= AnchorTop) ? MarginBottom : 0;

        switch (preset)
        {
            case LayoutAnchorPreset.None: // Treat as TopLeft with margins as size
                _anchorLeft = 0f; _anchorTop = 0f; _anchorRight = 0f; _anchorBottom = 0f;
                // Keep current margins if they were acting as position/size
                break;

            case LayoutAnchorPreset.TopLeft:
                _anchorLeft = 0f; _anchorTop = 0f; _anchorRight = 0f; _anchorBottom = 0f;
                // Margins define position and size. If size was set, keep it.
                _marginRight = (currentWidth > 0) ? currentWidth : _marginRight;
                _marginBottom = (currentHeight > 0) ? currentHeight : _marginBottom;
                break;
            case LayoutAnchorPreset.TopRight:
                _anchorLeft = 1f; _anchorTop = 0f; _anchorRight = 1f; _anchorBottom = 0f;
                _marginLeft = (currentWidth > 0) ? -currentWidth : _marginLeft; // Position from right
                _marginRight = 0; // Offset from right anchor
                _marginBottom = (currentHeight > 0) ? currentHeight : _marginBottom;
                break;
            case LayoutAnchorPreset.BottomLeft:
                _anchorLeft = 0f; _anchorTop = 1f; _anchorRight = 0f; _anchorBottom = 1f;
                _marginTop = (currentHeight > 0) ? -currentHeight : _marginTop; // Position from bottom
                _marginRight = (currentWidth > 0) ? currentWidth : _marginRight;
                _marginBottom = 0; // Offset from bottom anchor
                break;
            case LayoutAnchorPreset.BottomRight:
                _anchorLeft = 1f; _anchorTop = 1f; _anchorRight = 1f; _anchorBottom = 1f;
                _marginLeft = (currentWidth > 0) ? -currentWidth : _marginLeft;
                _marginTop = (currentHeight > 0) ? -currentHeight : _marginTop;
                _marginRight = 0; _marginBottom = 0;
                break;

            case LayoutAnchorPreset.CenterLeft:
                _anchorLeft = 0f; _anchorTop = 0.5f; _anchorRight = 0f; _anchorBottom = 0.5f;
                _marginTop = (currentHeight > 0) ? -currentHeight / 2f : _marginTop;
                _marginRight = (currentWidth > 0) ? currentWidth : _marginRight;
                _marginBottom = (currentHeight > 0) ? currentHeight / 2f : _marginBottom;
                break;
            case LayoutAnchorPreset.CenterRight:
                _anchorLeft = 1f; _anchorTop = 0.5f; _anchorRight = 1f; _anchorBottom = 0.5f;
                _marginLeft = (currentWidth > 0) ? -currentWidth : _marginLeft;
                _marginTop = (currentHeight > 0) ? -currentHeight / 2f : _marginTop;
                _marginRight = 0;
                _marginBottom = (currentHeight > 0) ? currentHeight / 2f : _marginBottom;
                break;
            case LayoutAnchorPreset.TopCenter:
                _anchorLeft = 0.5f; _anchorTop = 0f; _anchorRight = 0.5f; _anchorBottom = 0f;
                _marginLeft = (currentWidth > 0) ? -currentWidth / 2f : _marginLeft;
                _marginRight = (currentWidth > 0) ? currentWidth / 2f : _marginRight;
                _marginBottom = (currentHeight > 0) ? currentHeight : _marginBottom;
                break;
            case LayoutAnchorPreset.BottomCenter:
                _anchorLeft = 0.5f; _anchorTop = 1f; _anchorRight = 0.5f; _anchorBottom = 1f;
                _marginLeft = (currentWidth > 0) ? -currentWidth / 2f : _marginLeft;
                _marginTop = (currentHeight > 0) ? -currentHeight : _marginTop;
                _marginRight = (currentWidth > 0) ? currentWidth / 2f : _marginRight;
                _marginBottom = 0;
                break;
            case LayoutAnchorPreset.Center:
                _anchorLeft = 0.5f; _anchorTop = 0.5f; _anchorRight = 0.5f; _anchorBottom = 0.5f;
                _marginLeft = (currentWidth > 0) ? -currentWidth / 2f : _marginLeft;
                _marginTop = (currentHeight > 0) ? -currentHeight / 2f : _marginTop;
                _marginRight = (currentWidth > 0) ? currentWidth / 2f : _marginRight;
                _marginBottom = (currentHeight > 0) ? currentHeight / 2f : _marginBottom;
                break;

            case LayoutAnchorPreset.LeftWide:
                _anchorLeft = 0f; _anchorTop = 0f; _anchorRight = 0f; _anchorBottom = 1f;
                _marginRight = (currentWidth > 0) ? currentWidth : _marginRight; // Width
                _marginBottom = 0; // Relative to bottom anchor
                break;
            case LayoutAnchorPreset.RightWide:
                _anchorLeft = 1f; _anchorTop = 0f; _anchorRight = 1f; _anchorBottom = 1f;
                _marginLeft = (currentWidth > 0) ? -currentWidth : _marginLeft; // Positioned from right, width is -marginLeft
                _marginRight = 0; // Relative to right anchor
                _marginBottom = 0; // Relative to bottom anchor
                break;
            case LayoutAnchorPreset.TopWide:
                _anchorLeft = 0f; _anchorTop = 0f; _anchorRight = 1f; _anchorBottom = 0f;
                _marginRight = 0; // Relative to right anchor
                _marginBottom = (currentHeight > 0) ? currentHeight : _marginBottom; // Height
                break;
            case LayoutAnchorPreset.BottomWide:
                _anchorLeft = 0f; _anchorTop = 1f; _anchorRight = 1f; _anchorBottom = 1f;
                _marginTop = (currentHeight > 0) ? -currentHeight : _marginTop; // Positioned from bottom, height is -marginTop
                _marginRight = 0; // Relative to right anchor
                _marginBottom = 0; // Relative to bottom anchor
                break;

            case LayoutAnchorPreset.VCenterWide:
                _anchorLeft = 0f; _anchorTop = 0.5f; _anchorRight = 1f; _anchorBottom = 0.5f;
                _marginTop = (currentHeight > 0) ? -currentHeight / 2f : _marginTop;
                _marginRight = 0;
                _marginBottom = (currentHeight > 0) ? currentHeight / 2f : _marginBottom;
                break;
            case LayoutAnchorPreset.HCenterWide:
                _anchorLeft = 0.5f; _anchorTop = 0f; _anchorRight = 0.5f; _anchorBottom = 1f;
                _marginLeft = (currentWidth > 0) ? -currentWidth / 2f : _marginLeft;
                _marginRight = (currentWidth > 0) ? currentWidth / 2f : _marginRight;
                _marginBottom = 0;
                break;

            case LayoutAnchorPreset.FullRect:
                _anchorLeft = 0f; _anchorTop = 0f; _anchorRight = 1f; _anchorBottom = 1f;
                _marginLeft = 0; _marginTop = 0; _marginRight = 0; _marginBottom = 0; // Default to full coverage
                break;
        }
    }
}