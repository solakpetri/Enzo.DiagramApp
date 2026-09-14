namespace Enzo.Diagrams.Domain;

public static class FlowchartValidator
{
    public static IReadOnlyList<FlowchartValidationError> Validate(Flowchart flowchart)
    {
        var errors = new List<FlowchartValidationError>();
        var nodesById = new Dictionary<string, FlowchartNode>(StringComparer.Ordinal);

        foreach (var node in flowchart.Nodes)
        {
            if (nodesById.TryGetValue(node.Id, out var existingNode))
            {
                errors.Add(new FlowchartValidationError(
                    FlowchartValidationErrorKind.DuplicateNodeIdentifier,
                    node.Line,
                    node.Column,
                    $"Node identifier '{node.Id}' is already declared on line {existingNode.Line}.",
                    NodeId: node.Id));
                continue;
            }

            nodesById.Add(node.Id, node);
        }

        foreach (var edge in flowchart.Edges)
        {
            if (!nodesById.ContainsKey(edge.FromId))
            {
                errors.Add(UnknownNodeError(
                    FlowchartValidationErrorKind.UnknownEdgeSource,
                    edge,
                    edge.FromId,
                    nodesById.Keys));
            }

            if (!nodesById.ContainsKey(edge.ToId))
            {
                errors.Add(UnknownNodeError(
                    FlowchartValidationErrorKind.UnknownEdgeTarget,
                    edge,
                    edge.ToId,
                    nodesById.Keys));
            }
        }

        if (!flowchart.Nodes.Any(node => node.Kind == FlowchartNodeKind.Start))
        {
            errors.Add(new FlowchartValidationError(
                FlowchartValidationErrorKind.MissingStartNode,
                flowchart.Line,
                flowchart.Column,
                $"Diagram '{flowchart.Name}' must contain a start node."));
        }

        if (!flowchart.Nodes.Any(node => node.Kind == FlowchartNodeKind.End))
        {
            errors.Add(new FlowchartValidationError(
                FlowchartValidationErrorKind.MissingEndNode,
                flowchart.Line,
                flowchart.Column,
                $"Diagram '{flowchart.Name}' must contain an end node."));
        }

        if (errors.Count == 0)
        {
            DetectCycle(flowchart, errors);
        }

        return errors;
    }

    private static void DetectCycle(
        Flowchart flowchart,
        List<FlowchartValidationError> errors)
    {
        var outgoing = flowchart.Nodes.ToDictionary(
            node => node.Id,
            _ => new List<string>(),
            StringComparer.Ordinal);
        var incomingCounts = flowchart.Nodes.ToDictionary(
            node => node.Id,
            _ => 0,
            StringComparer.Ordinal);

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
                incomingCounts[targetId]--;
                if (incomingCounts[targetId] == 0)
                {
                    queue.Enqueue(targetId);
                }
            }
        }

        var cycleNode = flowchart.Nodes.FirstOrDefault(node => !processed.Contains(node.Id));
        if (cycleNode is null)
        {
            return;
        }

        errors.Add(new FlowchartValidationError(
            FlowchartValidationErrorKind.CycleDetected,
            cycleNode.Line,
            cycleNode.Column,
            $"Diagram '{flowchart.Name}' contains a cycle involving node '{cycleNode.Id}'.",
            NodeId: cycleNode.Id));
    }

    private static FlowchartValidationError UnknownNodeError(
        FlowchartValidationErrorKind kind,
        FlowchartEdge edge,
        string nodeId,
        IEnumerable<string> knownNodeIds)
    {
        var suggestion = FindClosestNodeId(nodeId, knownNodeIds);
        var message = $"Unknown node '{nodeId}' referenced by edge on line {edge.Line}.";
        if (suggestion is not null)
        {
            message += $" Did you mean '{suggestion}'?";
        }

        return new FlowchartValidationError(
            kind,
            edge.Line,
            edge.Column,
            message,
            NodeId: nodeId,
            EdgeFromId: edge.FromId,
            EdgeToId: edge.ToId,
            Suggestion: suggestion);
    }

    private static string? FindClosestNodeId(string nodeId, IEnumerable<string> knownNodeIds)
    {
        string? closest = null;
        var closestDistance = int.MaxValue;

        foreach (var knownNodeId in knownNodeIds)
        {
            if (Math.Abs(nodeId.Length - knownNodeId.Length) > 2)
            {
                continue;
            }

            var distance = LevenshteinDistance(nodeId, knownNodeId);
            if (distance < closestDistance)
            {
                closest = knownNodeId;
                closestDistance = distance;
            }
        }

        return closestDistance <= 2 ? closest : null;
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var column = 0; column <= right.Length; column++)
        {
            previous[column] = column;
        }

        for (var row = 1; row <= left.Length; row++)
        {
            current[0] = row;

            for (var column = 1; column <= right.Length; column++)
            {
                var cost = left[row - 1] == right[column - 1] ? 0 : 1;
                current[column] = Math.Min(
                    Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
