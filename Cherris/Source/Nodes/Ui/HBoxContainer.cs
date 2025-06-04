using System.Linq;
using System.Numerics;

namespace Cherris;

public class HBoxContainer : Node2D
{
    public float Separation { get; set; } = 4f;

    public override void Process()
    {
        base.Process();
        UpdateLayout();
    }

    private void UpdateLayout()
    {
        var visibleNode2DChildren = Children.OfType<Node2D>().Where(c => c.Visible).ToList();

        if (!visibleNode2DChildren.Any())
        {
            // If there are no children, and the current explicit size is not zero,
            // set it to zero using the public Size setter.
            if (_explicitSize != Vector2.Zero)
            {
                this.Size = Vector2.Zero;
            }
            return;
        }

        float totalChildrenWidth = 0;
        float calculatedContainerHeight = 0;

        foreach (Node2D child in visibleNode2DChildren)
        {
            totalChildrenWidth += child.Size.X;
            calculatedContainerHeight = Math.Max(calculatedContainerHeight, child.Size.Y);
        }

        float totalSeparation = (visibleNode2DChildren.Count > 1) ? (visibleNode2DChildren.Count - 1) * Separation : 0;
        float totalRequiredContentWidth = totalChildrenWidth + totalSeparation;

        Vector2 newShrinkWrapExplicitSize = new Vector2(totalRequiredContentWidth, calculatedContainerHeight);

        // Update the container's _explicitSize if it's meant to shrink-wrap to content
        // and is not controlled by relative sizing.
        if (RelativeWidth <= 0 && RelativeHeight <= 0)
        {
            // Only update if the calculated shrink-wrap size is different from the current explicit size.
            // This prevents unnecessary event invocations if the content size hasn't changed.
            if (_explicitSize != newShrinkWrapExplicitSize)
            {
                // Use the public 'Size' setter from Node2D. This will:
                // 1. Update _explicitSize to newShrinkWrapExplicitSize.
                // 2. Trigger the SizeChanged event with the *final calculated size*.
                this.Size = newShrinkWrapExplicitSize;
            }
        }
        // If RelativeWidth/Height are active, _explicitSize is used by the Size getter for the non-relative dimension,
        // or as a base if the relative calculation results in a smaller size than _explicitSize (depending on interpretation/implementation).
        // For now, we let the Node2D.Size getter handle the final size calculation.

        float containerDisplayWidth = this.Size.X; // Current actual width of the HBoxContainer (getter recalculates)
        float containerDisplayHeight = this.Size.Y; // Current actual height

        float initialContentOffsetX = 0;
        switch (this.HAlignment) // Use HBoxContainer's own HAlignment property
        {
            case HAlignment.Left:
                initialContentOffsetX = 0;
                break;
            case HAlignment.Center:
                initialContentOffsetX = (containerDisplayWidth - totalRequiredContentWidth) / 2f;
                break;
            case HAlignment.Right:
                initialContentOffsetX = containerDisplayWidth - totalRequiredContentWidth;
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
            switch (child.VAlignment)
            {
                case VAlignment.Top:
                    childY = 0;
                    break;
                case VAlignment.Center:
                    // Align child center to container's content area center (not necessarily container's visual center if padding/margins were involved)
                    childY = (containerDisplayHeight / 2f) - (child.Size.Y / 2f);
                    break;
                case VAlignment.Bottom:
                    childY = containerDisplayHeight - child.Size.Y;
                    break;
                case VAlignment.None:
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