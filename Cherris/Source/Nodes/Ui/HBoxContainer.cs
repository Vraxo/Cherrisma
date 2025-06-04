using System.Linq;
using System.Numerics;

namespace Cherris;

public class HBoxContainer : Node2D
{
    public float Separation { get; set; } = 4f;
    // Note: HBoxContainer inherits _explicitSize from Node2D (default 320,320)
    // If no Size is specified in YAML for HBoxContainer, it will use this default.

    public override void Process()
    {
        base.Process();
        UpdateLayout();
    }

    private void UpdateLayout()
    {
        var visibleNode2DChildren = Children.OfType<Node2D>().Where(c => c.Visible).ToList();

        float totalRequiredContentWidth = 0;
        float maxChildHeight = 0;

        if (visibleNode2DChildren.Any())
        {
            foreach (Node2D child in visibleNode2DChildren)
            {
                totalRequiredContentWidth += child.Size.X;
                maxChildHeight = Math.Max(maxChildHeight, child.Size.Y);
            }
            totalRequiredContentWidth += (visibleNode2DChildren.Count - 1) * Separation;
        }
        else // No visible children
        {
            // If HBoxContainer has no explicit size control (not relative, explicit is default/zero)
            // then its size should ideally be zero.
            if (RelativeWidth <= 0 && RelativeHeight <= 0 && _explicitSize != Vector2.Zero)
            {
                // this.Size = Vector2.Zero; // Set to zero if it was something else
            }
            // If it has children, layout continues below. If not, effectively done.
        }

        // Determine the container's actual rendering width and height for child layout.
        // This uses the Node2D.Size property, which considers _explicitSize (from YAML or Node2D default)
        // and RelativeWidth/Height.
        float currentContainerRenderWidth = this.Size.X;
        float currentContainerRenderHeight = this.Size.Y;

        // Adjust container's explicit height if it's not relatively sized and is smaller than content.
        // This allows the container to grow vertically to fit children.
        if (this.RelativeHeight == 0)
        {
            if (_explicitSize.Y < maxChildHeight) // Compare against _explicitSize.Y
            {
                // Update _explicitSize.Y to fit content and trigger SizeChanged
                this.Size = new Vector2(_explicitSize.X, maxChildHeight); // This updates _explicitSize via setter
                currentContainerRenderHeight = this.Size.Y; // Re-fetch potentially updated height
            }
            else if (visibleNode2DChildren.Any() && _explicitSize.Y == 0 && maxChildHeight > 0) // Handle if _explicitSize.Y was truly zero
            {
                this.Size = new Vector2(_explicitSize.X, maxChildHeight);
                currentContainerRenderHeight = this.Size.Y;
            }
        }
        // If no children and not relative, ensure explicit height is also cleared if width was cleared.
        // This is more complex if we want perfect shrink-to-zero.
        // For now, if no children, content width/height are 0, and layout below handles it.


        float initialContentOffsetX = 0;
        // Use the HBoxContainer's own HAlignment to position the block of children
        // within its currentContainerRenderWidth.
        switch (this.HAlignment)
        {
            case HAlignment.Left:
                initialContentOffsetX = 0;
                break;
            case HAlignment.Center:
                initialContentOffsetX = (currentContainerRenderWidth - totalRequiredContentWidth) / 2f;
                break;
            case HAlignment.Right:
                initialContentOffsetX = currentContainerRenderWidth - totalRequiredContentWidth;
                break;
            case HAlignment.None: // Treat as Left
            default:
                initialContentOffsetX = 0;
                break;
        }

        float currentX = initialContentOffsetX;
        foreach (Node2D child in visibleNode2DChildren)
        {
            float childY = 0;
            // Use child's VAlignment to position it vertically within the HBoxContainer's height.
            switch (child.VAlignment)
            {
                case VAlignment.Top:
                    childY = 0;
                    break;
                case VAlignment.Center:
                    childY = (currentContainerRenderHeight / 2f) - (child.Size.Y / 2f);
                    break;
                case VAlignment.Bottom:
                    childY = currentContainerRenderHeight - child.Size.Y;
                    break;
                case VAlignment.None: // Treat as Top
                default:
                    childY = 0;
                    break;
            }
            child.Position = new Vector2(currentX, childY);

            currentX += child.Size.X;
            if (visibleNode2DChildren.IndexOf(child) < visibleNode2DChildren.Count - 1)
            {
                currentX += Separation;
            }
        }
    }
}