namespace Enzo.Diagrams.Language;

public static class BpmnValidator
{
    public static IReadOnlyList<BpmnValidationError> Validate(BpmnDiagram diagram)
    {
        var errors = new List<BpmnValidationError>();
        var elementsById = new Dictionary<string, BpmnElement>(StringComparer.Ordinal);

        foreach (var element in diagram.Elements)
        {
            if (elementsById.TryGetValue(element.Id, out var existingElement))
            {
                errors.Add(new BpmnValidationError(
                    BpmnValidationErrorKind.DuplicateElementIdentifier,
                    element.Line,
                    element.Column,
                    $"BPMN element identifier '{element.Id}' is already declared on line {existingElement.Line}.",
                    ElementId: element.Id));
                continue;
            }

            elementsById.Add(element.Id, element);
        }

        foreach (var flow in diagram.Flows)
        {
            if (!elementsById.ContainsKey(flow.FromId))
            {
                errors.Add(UnknownFlowError(BpmnValidationErrorKind.UnknownFlowSource, flow, flow.FromId));
            }

            if (!elementsById.ContainsKey(flow.ToId))
            {
                errors.Add(UnknownFlowError(BpmnValidationErrorKind.UnknownFlowTarget, flow, flow.ToId));
            }
        }

        if (!diagram.Elements.Any(element => element.Kind == BpmnElementKind.StartEvent))
        {
            errors.Add(new BpmnValidationError(
                BpmnValidationErrorKind.MissingStartEvent,
                diagram.Line,
                diagram.Column,
                $"BPMN diagram '{diagram.Name}' must contain a start event."));
        }

        if (!diagram.Elements.Any(element => element.Kind == BpmnElementKind.EndEvent))
        {
            errors.Add(new BpmnValidationError(
                BpmnValidationErrorKind.MissingEndEvent,
                diagram.Line,
                diagram.Column,
                $"BPMN diagram '{diagram.Name}' must contain an end event."));
        }

        if (errors.Count == 0)
        {
            DetectCycle(diagram, errors);
        }

        return errors;
    }

    private static void DetectCycle(BpmnDiagram diagram, List<BpmnValidationError> errors)
    {
        var outgoing = diagram.Elements.ToDictionary(element => element.Id, _ => new List<string>(), StringComparer.Ordinal);
        var incomingCounts = diagram.Elements.ToDictionary(element => element.Id, _ => 0, StringComparer.Ordinal);

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
                incomingCounts[targetId]--;
                if (incomingCounts[targetId] == 0)
                {
                    queue.Enqueue(targetId);
                }
            }
        }

        var cycleElement = diagram.Elements.FirstOrDefault(element => !processed.Contains(element.Id));
        if (cycleElement is null)
        {
            return;
        }

        errors.Add(new BpmnValidationError(
            BpmnValidationErrorKind.CycleDetected,
            cycleElement.Line,
            cycleElement.Column,
            $"BPMN diagram '{diagram.Name}' contains a cycle involving element '{cycleElement.Id}'.",
            ElementId: cycleElement.Id));
    }

    private static BpmnValidationError UnknownFlowError(
        BpmnValidationErrorKind kind,
        BpmnSequenceFlow flow,
        string elementId)
    {
        return new BpmnValidationError(
            kind,
            flow.Line,
            flow.Column,
            $"Unknown BPMN element '{elementId}' referenced by sequence flow on line {flow.Line}.",
            ElementId: elementId,
            FlowFromId: flow.FromId,
            FlowToId: flow.ToId);
    }
}
