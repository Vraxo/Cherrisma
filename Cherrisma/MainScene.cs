using Cherris;

namespace Cherrisma;

public class MainScene : Node
{
    public override void Process()
    {
        base.Process();

        GetNode<HSlider>("HSlider").Position = GetWindowSizeV2() / 2;
    }
}