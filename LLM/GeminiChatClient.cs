using System.Net;
using System.Text;
using System.Text.Json;

namespace LocalCursorAgent.LLM
{
    /// <summary>
    /// Minimal Gemini client via Google Generative Language API.
    /// API key must be supplied via GEMINI_API_KEY.
    /// </summary>
    public sealed class GeminiChatClient : ILLMClient
    {
        private const int DefaultRequestTimeoutSeconds = 90;
        private readonly HttpClient _httpClient;
        private readonly string _model;
        private readonly string _endpoint;
        private readonly string _apiKey;

        public GeminiChatClient(string apiKey, string model = "gemini-1.5-flash", string endpointBase = "https://generativelanguage.googleapis.com")
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("Gemini API key is required.", nameof(apiKey));

            _apiKey = apiKey.Trim();
            _model = model;
            _endpoint = $"{endpointBase.TrimEnd('/')}/v1beta/models/{_model}:generateContent";
            _httpClient = new HttpClient { Timeout = ResolveRequestTimeout() };
        }

        public async Task<string> Generate(string prompt, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return "Error: Empty prompt";

            try
            {
                var payload = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                var requestUri = $"{_endpoint}?key={Uri.EscapeDataString(_apiKey)}";
                var response = await _httpClient.PostAsync(requestUri, content, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    var failureMessage = TryExtractGeminiErrorMessage(responseBody);
                    if (response.StatusCode == HttpStatusCode.TooManyRequests ||
                        (failureMessage?.Contains("quota", StringComparison.OrdinalIgnoreCase) ?? false))
                    {
                        return $"Error: Gemini unavailable due to quota or rate limits. {failureMessage}";
                    }

                    return string.IsNullOrWhiteSpace(failureMessage)
                        ? $"Error: Gemini returned status {response.StatusCode}"
                        : $"Error: Gemini returned status {response.StatusCode}. {failureMessage}";
                }

                using var jsonDoc = JsonDocument.Parse(responseBody);
                if (jsonDoc.RootElement.TryGetProperty("candidates", out var candidates) &&
                    candidates.ValueKind == JsonValueKind.Array &&
                    candidates.GetArrayLength() > 0 &&
                    candidates[0].TryGetProperty("content", out var contentElement) &&
                    contentElement.TryGetProperty("parts", out var partsElement) &&
                    partsElement.ValueKind == JsonValueKind.Array &&
                    partsElement.GetArrayLength() > 0 &&
                    partsElement[0].TryGetProperty("text", out var textElement))
                {
                    return textElement.GetString() ?? "Error: No response from Gemini";
                }

                return "Error: Unexpected response format from Gemini";
            }
            catch (HttpRequestException ex)
            {
                return $"Error: Unable to reach Gemini API. {ex.Message}";
            }
            catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                return $"Error: Gemini request canceled. {ex.Message}";
            }
            catch (TaskCanceledException ex)
            {
                return $"Error: Gemini request timed out. {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"Error: Gemini request failed. {ex.Message}";
            }
        }

        public async Task<bool> IsAvailable(CancellationToken cancellationToken = default)
        {
            try
            {
                var requestUri = $"https://generativelanguage.googleapis.com/v1beta/models?key={Uri.EscapeDataString(_apiKey)}";
                var response = await _httpClient.GetAsync(requestUri, cancellationToken);
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

        private static string? TryExtractGeminiErrorMessage(string responseBody)
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
                    var status = errorElement.TryGetProperty("status", out var statusElement)
                        ? statusElement.GetString()
                        : null;
                    if (!string.IsNullOrWhiteSpace(message) && !string.IsNullOrWhiteSpace(status))
                        return $"{message} ({status})";
                    return message ?? status;
                }
            }
            catch
            {
            }

            return null;
        }
    }
}
