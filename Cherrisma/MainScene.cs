using System.Numerics;
using Cherris;

namespace Cherrisma;

public class MainScene : Node
{
    public override void Ready()
    {
        base.Ready();
        var hSlider = GetNode<HSlider>("HSlider");

        if (hSlider != null)
        {
            // Set origin to top-left for easier anchor/margin reasoning
            hSlider.OriginPreset = OriginPreset.TopLeft;

            // Anchor to bottom edge, centered horizontally
            hSlider.Anchors = AnchorFlags.AnchorBottom | AnchorFlags.CenterHorizontal;

            // Set margin from bottom edge
            hSlider.MarginBottom = 32f;

            // Set horizontal margins for CenterHorizontal (these act as offsets from true center)
            // For true centering with no additional offset, margins can be 0 or not set if CenterHorizontal handles it.
            // If CenterHorizontal uses MarginLeft/Right for width adjustment or fine-tuning:
            // hSlider.MarginLeft = 0; 
            // hSlider.MarginRight = 0;

            // The Position property is now an offset from the anchor point.
            // For pure anchoring to the calculated spot, set Position to Zero.
            hSlider.Position = Vector2.Zero;
        }
    }

    public override void Process()
    {
        base.Process();
        // The HSlider's position is now managed by its anchors and margins.
        // No need to manually set GetNode<HSlider>("HSlider").Position here anymore if anchors are used.
        // If you still needed to adjust something dynamically that anchors don't cover,
        // you might change margins or the Position (offset) property.
    }
}