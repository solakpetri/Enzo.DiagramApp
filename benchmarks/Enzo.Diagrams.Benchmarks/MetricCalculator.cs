using System.Text;
using SharpToken;

namespace Enzo.Diagrams.Benchmarks;

public sealed class MetricCalculator
{
    private readonly GptEncoding _encoding;

    public MetricCalculator(string encodingName)
    {
        _encoding = GptEncoding.GetEncoding(encodingName);
    }

    public RepresentationMetrics Calculate(string source, int elementCount, int connectionCount)
    {
        var tokens = _encoding.Encode(source).Count;

        return new RepresentationMetrics(
            Encoding.UTF8.GetByteCount(source),
            source.Length,
            source.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Count(line => !string.IsNullOrWhiteSpace(line)),
            tokens,
            elementCount == 0 ? null : (double)tokens / elementCount,
            connectionCount == 0 ? null : (double)tokens / connectionCount);
    }
}
