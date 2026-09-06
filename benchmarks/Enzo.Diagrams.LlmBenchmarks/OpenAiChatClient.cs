using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Enzo.Diagrams.LlmBenchmarks;

public sealed class OpenAiChatClient(HttpClient httpClient, string apiKey) : IDiagramModelClient
{
    public async Task<ModelResponse> CompleteAsync(string systemPrompt, string userPrompt, ModelSettings settings, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent(settings, systemPrompt, userPrompt);
        var stopwatch = Stopwatch.StartNew();
        using var response = await SendWithRetriesAsync(request, cancellationToken);
        stopwatch.Stop();
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"OpenAI API returned {(int)response.StatusCode}.");
        }

        using var json = JsonDocument.Parse(body);
        var source = json.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        return new ModelResponse(source, OpenAiUsageParser.Extract(body), stopwatch.Elapsed);
    }

    private async Task<HttpResponseMessage> SendWithRetriesAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var clone = await CloneAsync(request, cancellationToken);
            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(clone, cancellationToken);
            }
            catch (HttpRequestException) when (attempt < 2)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
                continue;
            }

            if (!IsTransient(response.StatusCode) || attempt == 2)
            {
                return response;
            }

            response.Dispose();
            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
        }
    }

    private static StringContent JsonContent(ModelSettings settings, string systemPrompt, string userPrompt)
    {
        var body = JsonSerializer.Serialize(new
        {
            model = settings.Model,
            temperature = settings.Temperature,
            top_p = settings.TopP,
            max_tokens = settings.MaxOutputTokens,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        });
        return new StringContent(body, Encoding.UTF8, "application/json");
    }

    private static bool IsTransient(HttpStatusCode statusCode) => statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests or >= HttpStatusCode.InternalServerError;

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        clone.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return clone;
    }
}

public static class OpenAiUsageParser
{
    public static TokenUsage Extract(string responseJson)
    {
        using var json = JsonDocument.Parse(responseJson);
        var usage = json.RootElement.GetProperty("usage");
        return new TokenUsage(
            usage.GetProperty("prompt_tokens").GetInt32(),
            usage.GetProperty("completion_tokens").GetInt32(),
            usage.GetProperty("total_tokens").GetInt32());
    }
}
