using System.Numerics;
using Cherris;

namespace Cherrisma;

public class MainScene : Node
{
    public override void Process()
    {
        base.Process();

        Vector2 windowSize = GetWindowSizeV2();
        // This log will now help confirm if MainScene.Process is being called during resize
        // and what window size it perceives.
        Log.Info($"MainScene.Process: WindowSize from GetWindowSizeV2(): {windowSize}. HSlider is being updated.");

        var hSlider = GetNodeOrNull<HSlider>("HSlider");
        if (hSlider != null)
        {
            Vector2 oldPos = hSlider.Position;
            hSlider.Position = windowSize / 2;
            if (oldPos != hSlider.Position)
            {
                Log.Info($"MainScene.Process: HSlider LocalPos changed from {oldPos} to {hSlider.Position} based on WindowSize {windowSize}");
            }
            else if (hSlider.Position != windowSize / 2)
            {
                // This case might occur if hSlider.Position was already windowSize/2 but something else changed its GlobalPosition effectively
                Log.Info($"MainScene.Process: HSlider LocalPos IS {hSlider.Position}, which should be WindowSize/2 ({windowSize / 2}). GlobalPos: {hSlider.GlobalPosition}");
            }
        }
        else
        {
            Log.Warning("MainScene.Process: HSlider node not found.");
        }
    }
}