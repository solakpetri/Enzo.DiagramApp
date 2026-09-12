namespace Enzo.Diagrams.Domain;

public static class BpmnLayoutEngine
{
    private const double EventSize = 64;
    private const double TaskWidth = 160;
    private const double TaskHeight = 64;
    private const double GatewaySize = 96;
    private const double HorizontalSpacing = 80;
    private const double VerticalSpacing = 96;
    private const double Margin = 40;

    public static BpmnLayout Layout(BpmnDiagram diagram)
    {
        if (BpmnValidator.Validate(diagram).Count > 0)
        {
            throw new ArgumentException("BPMN diagram must be valid.", nameof(diagram));
        }

        if (diagram.Elements.Count == 0)
        {
            return new BpmnLayout([], [], new DiagramBounds(0, 0, 0, 0));
        }

        var layers = AssignLayers(diagram);
        var unnormalizedElements = PositionElements(diagram, layers);
        var contentBounds = CalculateContentBounds(unnormalizedElements);
        var offset = new DiagramPoint(Margin - contentBounds.X, Margin - contentBounds.Y);
        var elements = unnormalizedElements
            .Select(element => element with { X = element.X + offset.X, Y = element.Y + offset.Y })
            .ToList();
        var elementsById = elements.ToDictionary(element => element.Element.Id, StringComparer.Ordinal);
        var flows = PositionFlows(diagram, elementsById);
        var bounds = new DiagramBounds(0, 0, contentBounds.Width + (Margin * 2), contentBounds.Height + (Margin * 2));

        return new BpmnLayout(elements, flows, bounds);
    }

    private static Dictionary<string, int> AssignLayers(BpmnDiagram diagram)
    {
        var outgoing = diagram.Elements.ToDictionary(element => element.Id, _ => new List<string>(), StringComparer.Ordinal);
        var incomingCounts = diagram.Elements.ToDictionary(element => element.Id, _ => 0, StringComparer.Ordinal);
        var layers = diagram.Elements.ToDictionary(element => element.Id, _ => 0, StringComparer.Ordinal);

        foreach (var flow in diagram.Flows)
        {
            outgoing[flow.FromId].Add(flow.ToId);
            incomingCounts[flow.ToId]++;
        }

        var queue = new Queue<string>(diagram.Elements
            .Where(element => incomingCounts[element.Id] == 0)
            .Select(element => element.Id));
        var processed = new HashSet<string>(StringComparer.Ordinal);

        while (queue.Count > 0)
        {
            var elementId = queue.Dequeue();
            processed.Add(elementId);

            foreach (var targetId in outgoing[elementId])
            {
                layers[targetId] = Math.Max(layers[targetId], layers[elementId] + 1);
                incomingCounts[targetId]--;

                if (incomingCounts[targetId] == 0)
                {
                    queue.Enqueue(targetId);
                }
            }
        }

        if (processed.Count != diagram.Elements.Count)
        {
            throw new InvalidOperationException("BPMN diagram contains a cycle.");
        }

        return layers;
    }

    private static List<PositionedBpmnElement> PositionElements(
        BpmnDiagram diagram,
        IReadOnlyDictionary<string, int> layers)
    {
        var positionedElements = new List<PositionedBpmnElement>();

        foreach (var layer in diagram.Elements.GroupBy(element => layers[element.Id]).OrderBy(group => group.Key))
        {
            var elements = layer.ToList();
            var widths = elements.Select(element => SizeFor(element).Width).ToList();
            var totalWidth = widths.Sum() + ((elements.Count - 1) * HorizontalSpacing);
            var maxHeight = elements.Max(element => SizeFor(element).Height);
            var x = -totalWidth / 2;
            var y = layer.Key * (maxHeight + VerticalSpacing);

            for (var index = 0; index < elements.Count; index++)
            {
                var size = SizeFor(elements[index]);
                positionedElements.Add(new PositionedBpmnElement(elements[index], x, y + ((maxHeight - size.Height) / 2), size));
                x += size.Width + HorizontalSpacing;
            }
        }

        return positionedElements;
    }

    private static List<PositionedBpmnSequenceFlow> PositionFlows(
        BpmnDiagram diagram,
        IReadOnlyDictionary<string, PositionedBpmnElement> elementsById)
    {
        var flows = new List<PositionedBpmnSequenceFlow>();

        foreach (var flow in diagram.Flows)
        {
            var from = elementsById[flow.FromId];
            var to = elementsById[flow.ToId];
            var start = new DiagramPoint(from.X + (from.Size.Width / 2), from.Y + from.Size.Height);
            var end = new DiagramPoint(to.X + (to.Size.Width / 2), to.Y);
            IReadOnlyList<DiagramPoint> points = start.X == end.X
                ? [start, end]
                : [start, new DiagramPoint(start.X, (start.Y + end.Y) / 2), new DiagramPoint(end.X, (start.Y + end.Y) / 2), end];

            flows.Add(new PositionedBpmnSequenceFlow(flow, points));
        }

        return flows;
    }

    private static DiagramBounds CalculateContentBounds(IReadOnlyList<PositionedBpmnElement> elements)
    {
        var minX = elements.Min(element => element.X);
        var minY = elements.Min(element => element.Y);
        var maxX = elements.Max(element => element.X + element.Size.Width);
        var maxY = elements.Max(element => element.Y + element.Size.Height);

        return new DiagramBounds(minX, minY, maxX - minX, maxY - minY);
    }

    private static BpmnElementSize SizeFor(BpmnElement element)
    {
        return element.Kind switch
        {
            BpmnElementKind.Task => new BpmnElementSize(TaskWidth, TaskHeight),
            BpmnElementKind.ExclusiveGateway => new BpmnElementSize(GatewaySize, GatewaySize),
            _ => new BpmnElementSize(EventSize, EventSize)
        };
    }
}
