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

    protected override Vector2 ComputeAutoSize()
    {
        var visibleNode2DChildren = Children.OfType<Node2D>().Where(c => c.Visible).ToList();
        float requiredWidth = 0;
        float maxHeight = 0;

        if (visibleNode2DChildren.Any())
        {
            foreach (Node2D child in visibleNode2DChildren)
            {
                requiredWidth += child.Size.X;
                maxHeight = Math.Max(maxHeight, child.Size.Y);
            }
            requiredWidth += (visibleNode2DChildren.Count - 1) * Separation;
        }
        return new Vector2(requiredWidth, maxHeight);
    }

    private void UpdateLayout()
    {
        var visibleNode2DChildren = Children.OfType<Node2D>().Where(c => c.Visible).ToList();

        // Use this.Size which now correctly reflects explicit, relative, or auto-sized dimensions
        float currentContainerRenderWidth = this.Size.X;
        float currentContainerRenderHeight = this.Size.Y;

        float totalRequiredContentWidth = 0;
        if (visibleNode2DChildren.Any())
        {
            foreach (Node2D child in visibleNode2DChildren)
            {
                totalRequiredContentWidth += child.Size.X;
            }
            totalRequiredContentWidth += (visibleNode2DChildren.Count - 1) * Separation;
        }


        float initialContentOffsetX = 0;
        switch (this.HAlignment) // This HAlignment is for aligning the group of children
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
            case HAlignment.None:
            default:
                initialContentOffsetX = 0;
                break;
        }

        float currentX = initialContentOffsetX;
        foreach (Node2D child in visibleNode2DChildren)
        {
            float childY = 0;
            switch (child.VAlignment) // Child's VAlignment for its position within container's height
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
                case VAlignment.None:
                default:
                    childY = 0;
                    break;
            }
            // Position is relative to the HBoxContainer's origin
            child.Position = new Vector2(currentX, childY);

            currentX += child.Size.X;
            if (visibleNode2DChildren.IndexOf(child) < visibleNode2DChildren.Count - 1)
            {
                currentX += Separation;
            }
        }
    }
}