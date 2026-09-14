namespace Enzo.Diagrams.Domain;

public static class FlowchartLayoutEngine
{
    private const double NodeWidth = 160;
    private const double NodeHeight = 64;
    private const double HorizontalSpacing = 80;
    private const double VerticalSpacing = 96;
    private const double Margin = 40;

    private static readonly FlowchartNodeSize DefaultNodeSize = new(NodeWidth, NodeHeight);

    public static FlowchartLayout Layout(Flowchart flowchart)
    {
        if (FlowchartValidator.Validate(flowchart).Count > 0)
        {
            throw new ArgumentException("Flowchart must be valid.", nameof(flowchart));
        }

        if (flowchart.Nodes.Count == 0)
        {
            return new FlowchartLayout([], [], new DiagramBounds(0, 0, 0, 0));
        }

        var layers = AssignLayers(flowchart);
        var unnormalizedNodes = PositionNodes(flowchart, layers);
        var contentBounds = CalculateContentBounds(unnormalizedNodes);
        var offset = new DiagramPoint(Margin - contentBounds.X, Margin - contentBounds.Y);
        var nodes = NormalizeNodes(unnormalizedNodes, offset);
        var nodesById = nodes.ToDictionary(node => node.Node.Id, StringComparer.Ordinal);
        var connections = PositionConnections(flowchart, nodesById);
        var canvasBounds = new DiagramBounds(
            0,
            0,
            contentBounds.Width + (Margin * 2),
            contentBounds.Height + (Margin * 2));

        return new FlowchartLayout(nodes, connections, canvasBounds);
    }

    private static Dictionary<string, int> AssignLayers(Flowchart flowchart)
    {
        var outgoing = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var incomingCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var layers = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var node in flowchart.Nodes)
        {
            outgoing.Add(node.Id, []);
            incomingCounts.Add(node.Id, 0);
            layers.Add(node.Id, 0);
        }

        foreach (var edge in flowchart.Edges)
        {
            outgoing[edge.FromId].Add(edge.ToId);
            incomingCounts[edge.ToId]++;
        }

        var queue = new Queue<string>(flowchart.Nodes
            .Where(node => incomingCounts[node.Id] == 0)
            .Select(node => node.Id));
        var processed = new HashSet<string>(StringComparer.Ordinal);

        while (queue.Count > 0)
        {
            var nodeId = queue.Dequeue();
            processed.Add(nodeId);

            foreach (var targetId in outgoing[nodeId])
            {
                layers[targetId] = Math.Max(layers[targetId], layers[nodeId] + 1);
                incomingCounts[targetId]--;

                if (incomingCounts[targetId] == 0)
                {
                    queue.Enqueue(targetId);
                }
            }
        }

        if (processed.Count != flowchart.Nodes.Count)
        {
            throw new InvalidOperationException("Flowchart contains a cycle.");
        }

        return layers;
    }

    private static List<PositionedFlowchartNode> PositionNodes(
        Flowchart flowchart,
        IReadOnlyDictionary<string, int> layers)
    {
        var positionedNodes = new List<PositionedFlowchartNode>();

        foreach (var layer in flowchart.Nodes.GroupBy(node => layers[node.Id]).OrderBy(group => group.Key))
        {
            var nodes = layer.ToList();
            var totalWidth = (nodes.Count * NodeWidth) + ((nodes.Count - 1) * HorizontalSpacing);
            var x = -totalWidth / 2;
            var y = layer.Key * (NodeHeight + VerticalSpacing);

            foreach (var node in nodes)
            {
                positionedNodes.Add(new PositionedFlowchartNode(node, x, y, DefaultNodeSize));
                x += NodeWidth + HorizontalSpacing;
            }
        }

        return positionedNodes;
    }

    private static DiagramBounds CalculateContentBounds(IReadOnlyList<PositionedFlowchartNode> nodes)
    {
        var minX = nodes.Min(node => node.X);
        var minY = nodes.Min(node => node.Y);
        var maxX = nodes.Max(node => node.X + node.Size.Width);
        var maxY = nodes.Max(node => node.Y + node.Size.Height);

        return new DiagramBounds(
            minX,
            minY,
            maxX - minX,
            maxY - minY);
    }

    private static List<PositionedFlowchartNode> NormalizeNodes(
        IReadOnlyList<PositionedFlowchartNode> nodes,
        DiagramPoint offset)
    {
        return nodes
            .Select(node => node with
            {
                X = node.X + offset.X,
                Y = node.Y + offset.Y
            })
            .ToList();
    }

    private static List<PositionedFlowchartConnection> PositionConnections(
        Flowchart flowchart,
        IReadOnlyDictionary<string, PositionedFlowchartNode> nodesById)
    {
        var connections = new List<PositionedFlowchartConnection>();

        foreach (var edge in flowchart.Edges)
        {
            var from = nodesById[edge.FromId];
            var to = nodesById[edge.ToId];
            var start = new DiagramPoint(from.X + (from.Size.Width / 2), from.Y + from.Size.Height);
            var end = new DiagramPoint(to.X + (to.Size.Width / 2), to.Y);

            IReadOnlyList<DiagramPoint> points = start.X == end.X
                ? [start, end]
                : [start, new DiagramPoint(start.X, (start.Y + end.Y) / 2), new DiagramPoint(end.X, (start.Y + end.Y) / 2), end];

            connections.Add(new PositionedFlowchartConnection(edge, points));
        }

        return connections;
    }
}
