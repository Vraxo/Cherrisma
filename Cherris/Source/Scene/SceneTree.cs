using System.Runtime.CompilerServices;

namespace Cherris;

public sealed class SceneTree
{
    public static SceneTree Instance { get; } = new();
    public object SyncRoot { get; } = new object(); // For thread synchronization

    public Node? RootNode { get; set; }
    public bool Paused { get; set; }

    private readonly ConditionalWeakTable<Node, object> readyNodes = [];
    private readonly List<SceneTreeTimer> timers = [];
    private readonly List<Tween> activeTweens = [];

    private SceneTree() { }

    public void Process()
    {
        if (RootNode is null)
        {
            return;
        }

        ProcessNode(RootNode);

        ProcessTweens();

        if (!Paused)
        {
            ProcessTimers();
        }
    }

    public void RenderScene(DrawingContext context)
    {
        if (RootNode is null)
        {
            return;
        }

        RenderNode(RootNode, context);
    }

    private void ProcessNode(Node node)
    {
        if (node is null || !node.Active)
        {
            return;
        }

        Node.ProcessMode effectiveMode = ComputeEffectiveProcessMode(node);
        bool shouldProcess = ShouldProcess(effectiveMode);

        if (shouldProcess)
        {
            EnsureNodeReady(node);
            node.ProcessBegin();
            node.Process();
        }


        var childrenToProcess = new List<Node>(node.Children);
        foreach (Node child in childrenToProcess)
        {
            ProcessNode(child);
        }

        if (shouldProcess)
        {
            node.ProcessEnd();
        }
    }

    private void EnsureNodeReady(Node node)
    {
        if (!readyNodes.TryGetValue(node, out _))
        {
            node.Ready();
            readyNodes.Add(node, null);
        }
    }

    private static Node.ProcessMode ComputeEffectiveProcessMode(Node node)
    {
        if (node.ProcessingMode != Node.ProcessMode.Inherit)
        {
            return node.ProcessingMode;
        }

        Node? current = node.Parent;

        while (current != null)
        {
            if (current.ProcessingMode != Node.ProcessMode.Inherit)
            {
                return current.ProcessingMode;
            }

            current = current.Parent;
        }

        return Node.ProcessMode.Pausable;
    }

    private bool ShouldProcess(Node.ProcessMode mode) => mode switch
    {
        Node.ProcessMode.Disabled => false,
        Node.ProcessMode.Always => true,
        Node.ProcessMode.Pausable => !Paused,
        Node.ProcessMode.WhenPaused => Paused,
        _ => false
    };

    private static void RenderNode(Node node, DrawingContext context)
    {

        if (node is WindowNode)
        {

            return;
        }

        if (node is VisualItem { Visible: true } visualItem)
        {
            visualItem.Draw(context);
        }


        var childrenToRender = new List<Node>(node.Children);
        foreach (Node child in childrenToRender)
        {
            RenderNode(child, context);
        }
    }

    public SceneTreeTimer CreateTimer(float time)
    {
        SceneTreeTimer timer = new(time);
        timers.Add(timer);
        return timer;
    }

    public void RemoveTimer(SceneTreeTimer timer)
    {
        timers.Remove(timer);
    }

    private void ProcessTimers()
    {
        var timersToProcess = new List<SceneTreeTimer>(timers);
        foreach (SceneTreeTimer timer in timersToProcess)
        {
            timer.Process();
        }
    }

    public void ChangeScene(Node node)
    {
        // Ensure thread safety if ChangeScene can be called from different threads
        lock (SyncRoot)
        {
            RootNode?.Free();
            RootNode = node;
            readyNodes.Clear(); // This needs to be careful with multi-threading
            // Consider if activeTweens and timers also need clearing or adjustment
        }
    }

    public Tween CreateTween(Node creatorNode, Node.ProcessMode processMode = Node.ProcessMode.Inherit)
    {
        Tween tween = new(creatorNode, processMode);
        // Consider thread safety for activeTweens list if tweens can be created from multiple threads
        lock (SyncRoot) // Or a dedicated lock for tweens
        {
            activeTweens.Add(tween);
        }
        return tween;
    }

    private void ProcessTweens()
    {
        // Create a copy for safe iteration, especially if tweens can be removed during iteration
        List<Tween> tweensToProcess;
        lock (SyncRoot) // Or a dedicated lock for tweens
        {
            tweensToProcess = new List<Tween>(activeTweens);
        }

        foreach (Tween tween in tweensToProcess)
        {
            if (!tween.Active) // Check Active status outside the lock to avoid holding it too long
            {
                lock (SyncRoot) { activeTweens.Remove(tween); }
                continue;
            }

            if (tween.ShouldProcess(Paused))
            {
                tween.Update(Time.Delta); // Time.Delta is now updated by the logic thread
            }

            if (!tween.Active) // Re-check after update
            {
                lock (SyncRoot) { activeTweens.Remove(tween); }
            }
        }
    }
}