using System.Text;
using System.Text.Json;
using System.Net;

namespace LocalCursorAgent.LLM
{
    /// <summary>
    /// Client for OpenAI Chat Completions API.
    /// API key must be supplied via OPENAI_API_KEY.
    /// </summary>
public sealed class OpenAIChatClient : ILLMClient
{
    private const int DefaultRequestTimeoutSeconds = 90;
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly string _endpoint;

        public OpenAIChatClient(string apiKey, string model = "gpt-4.1-mini", string endpoint = "https://api.openai.com/v1/chat/completions")
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("OpenAI API key is required.", nameof(apiKey));

            _model = model;
            _endpoint = endpoint;
            _httpClient = new HttpClient { Timeout = ResolveRequestTimeout() };
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        }

        public async Task<string> Generate(string prompt, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return "Error: Empty prompt";

            try
            {
                var requestPayload = new
                {
                    model = _model,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.2,
                    stream = false
                };

                var json = JsonSerializer.Serialize(requestPayload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_endpoint, content, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    var failureMessage = TryExtractOpenAiErrorMessage(responseBody);
                    if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                        (failureMessage?.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase) ?? false))
                    {
                        return $"Error: OpenAI unavailable due to quota or rate limits. {failureMessage}";
                    }

                    return string.IsNullOrWhiteSpace(failureMessage)
                        ? $"Error: OpenAI returned status {response.StatusCode}"
                        : $"Error: OpenAI returned status {response.StatusCode}. {failureMessage}";
                }

                var successBody = await response.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(successBody);

                if (jsonDoc.RootElement.TryGetProperty("choices", out var choices) &&
                    choices.GetArrayLength() > 0 &&
                    choices[0].TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var contentElement))
                {
                    return contentElement.GetString() ?? "Error: No response from OpenAI";
                }

                return "Error: Unexpected response format from OpenAI";
            }
            catch (HttpRequestException ex)
            {
                return $"Error: Unable to reach OpenAI API. {ex.Message}";
            }
            catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                return $"Error: OpenAI request canceled. {ex.Message}";
            }
            catch (TaskCanceledException ex)
            {
                return $"Error: OpenAI request timed out. {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"Error: OpenAI request failed. {ex.Message}";
            }
        }

        public async Task<bool> IsAvailable(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync("https://api.openai.com/v1/models", cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private static TimeSpan ResolveRequestTimeout()
        {
            var raw = Environment.GetEnvironmentVariable("LOCALCURSOR_LLM_TIMEOUT_SECONDS");
            return int.TryParse(raw, out var seconds) && seconds > 0
                ? TimeSpan.FromSeconds(seconds)
                : TimeSpan.FromSeconds(DefaultRequestTimeoutSeconds);
        }

        private static string? TryExtractOpenAiErrorMessage(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
                return null;

            try
            {
                using var jsonDoc = JsonDocument.Parse(responseBody);
                if (jsonDoc.RootElement.TryGetProperty("error", out var errorElement))
                {
                    var message = errorElement.TryGetProperty("message", out var messageElement)
                        ? messageElement.GetString()
                        : null;
                    var code = errorElement.TryGetProperty("code", out var codeElement)
                        ? codeElement.GetString()
                        : null;
                    if (!string.IsNullOrWhiteSpace(message) && !string.IsNullOrWhiteSpace(code))
                        return $"{message} ({code})";
                    return message ?? code;
                }
            }
            catch
            {
                // Ignore malformed error payloads and fall back to status-based handling.
            }

            return null;
        }
    }
}
