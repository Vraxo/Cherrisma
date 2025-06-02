using Cherris;

namespace Cherrisma;

public class MainScene : Node
{
    public override void Process()
    {
        base.Process();

        // The HSlider's position will now be determined by its AnchorPreset and Margin
        // properties, as defined in the Res/Main.yaml file.
        // The line below is removed to prevent overriding the declarative layout.
        // GetNode<HSlider>("Slider").Position = GetWindowSizeV2() / 2;
    }
}