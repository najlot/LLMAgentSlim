using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LLMAgentSlim;

public class HttpPlugin(string workspaceRoot, HttpClient httpClient) : WorkspacePluginBase(workspaceRoot)
{
    [KernelFunction("send_request")]
    [Description("Sends an HTTP request for API testing and returns the status, response headers, and response body. Headers can be provided either as JSON like {\"Authorization\":\"Bearer ...\"} or as one 'Name: Value' pair per line.")]
    public Task<string> SendRequest(
        [Description("Absolute http or https URL.")] string url,
        [Description("HTTP method such as GET, POST, PUT, PATCH, DELETE, HEAD, or OPTIONS.")] string method = "GET",
        [Description("Optional headers as JSON or one 'Name: Value' pair per line.")] string headers = "",
        [Description("Optional request body text.")] string body = "",
        [Description("Content type for the request body when Content-Type is not included in headers.")] string contentType = "application/json",
        [Description("Timeout in seconds.")] int timeoutSeconds = 60) =>
        ExecuteAsync(async () =>
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return "URL must be an absolute http or https URL.";
            }

            if (string.IsNullOrWhiteSpace(method))
            {
                return "HTTP method cannot be empty.";
            }

            var parsedHeaders = ParseHeaders(headers);
            var effectiveTimeout = Math.Clamp(timeoutSeconds, 1, 600);
            using var request = new HttpRequestMessage(new HttpMethod(method.Trim().ToUpperInvariant()), uri);
            var explicitContentType = GetHeaderValue(parsedHeaders, "Content-Type");

            if (!string.IsNullOrEmpty(body))
            {
                request.Content = new StringContent(
                    body,
                    Encoding.UTF8,
                    string.IsNullOrWhiteSpace(explicitContentType) ? contentType : explicitContentType);
            }

            foreach (var (name, value) in parsedHeaders)
            {
                if (string.Equals(name, "Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    if (request.Content is null)
                    {
                        continue;
                    }

                    request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(value);
                    continue;
                }

                if (!request.Headers.TryAddWithoutValidation(name, value))
                {
                    if (request.Content is null || !request.Content.Headers.TryAddWithoutValidation(name, value))
                    {
                        return $"Header '{name}' is not valid for this request.";
                    }
                }
            }

            var stopwatch = Stopwatch.StartNew();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(effectiveTimeout));

            try
            {
                using var response = await httpClient.SendAsync(request, timeout.Token).ConfigureAwait(false);
                var responseBody = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
                stopwatch.Stop();

                var builder = new StringBuilder();
                builder.AppendLine($"Method: {request.Method}");
                builder.AppendLine($"URL: {uri}");
                builder.AppendLine($"Duration: {stopwatch.ElapsedMilliseconds} ms");
                builder.AppendLine($"Status: {(int)response.StatusCode} {response.ReasonPhrase}");
                builder.AppendLine("Headers:");
                builder.AppendLine(FormatHeaders(response));
                builder.AppendLine("Body:");
                builder.AppendLine(string.IsNullOrEmpty(responseBody) ? "(empty)" : responseBody);

                return PluginOutputFormatter.Limit(builder.ToString().TrimEnd());
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                return $"Request timed out after {effectiveTimeout} seconds.";
            }
        });

    private static List<KeyValuePair<string, string>> ParseHeaders(string headers)
    {
        if (string.IsNullOrWhiteSpace(headers))
        {
            return [];
        }

        var trimmedHeaders = headers.Trim();
        if (trimmedHeaders.StartsWith("{", StringComparison.Ordinal))
        {
            try
            {
                using var document = JsonDocument.Parse(trimmedHeaders);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidOperationException("Headers JSON must be an object.");
                }

                var result = new List<KeyValuePair<string, string>>();
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in property.Value.EnumerateArray())
                        {
                            result.Add(new KeyValuePair<string, string>(property.Name, item.ToString()));
                        }
                        continue;
                    }

                    result.Add(new KeyValuePair<string, string>(property.Name, property.Value.ToString()));
                }

                return result;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Headers JSON is invalid: {ex.Message}", ex);
            }
        }

        var parsedHeaders = new List<KeyValuePair<string, string>>();
        foreach (var line in trimmedHeaders.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                throw new InvalidOperationException($"Invalid header '{line}'. Use 'Name: Value'.");
            }

            var name = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            parsedHeaders.Add(new KeyValuePair<string, string>(name, value));
        }

        return parsedHeaders;
    }

    private static string? GetHeaderValue(IEnumerable<KeyValuePair<string, string>> headers, string name)
    {
        string? value = null;
        foreach (var header in headers)
        {
            if (string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                value = header.Value;
            }
        }

        return value;
    }

    private static string FormatHeaders(HttpResponseMessage response)
    {
        var lines = response.Headers
            .Concat(response.Content.Headers)
            .SelectMany(header => header.Value.Select(value => $"{header.Key}: {value}"))
            .ToList();

        return lines.Count == 0
            ? "(none)"
            : string.Join(Environment.NewLine, lines);
    }
}
