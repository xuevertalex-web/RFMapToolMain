using System.Text.Json;

namespace LocalCursorAgent.LLM
{
    /// <summary>
    /// Client for communicating with Ollama API.
    /// </summary>
    public class OllamaClient : ILLMClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _model;
        private readonly string _endpoint;
        private readonly string _keepAlive;

        public OllamaClient(string endpoint = "http://localhost:11434", string model = "qwen2.5-coder:7b")
        {
            _endpoint = endpoint;
            _model = model;
            _keepAlive = ResolveKeepAlive();
            _httpClient = new HttpClient
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
        }

        /// <summary>
        /// Send a prompt to the LLM and get a response.
        /// </summary>
        public async Task<string> Generate(string prompt, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return "Error: Empty prompt";

            try
            {
                var requestPayload = new
                {
                    model = _model,
                    prompt,
                    stream = false,
                    keep_alive = _keepAlive
                };

                var json = JsonSerializer.Serialize(requestPayload);
                using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_endpoint}/api/generate", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                    return $"Error: LLM returned status {response.StatusCode}";

                var responseBody = await response.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(responseBody);

                if (jsonDoc.RootElement.TryGetProperty("response", out var responseElement))
                {
                    return responseElement.GetString() ?? "Error: No response from LLM";
                }

                return "Error: Unexpected response format from LLM";
            }
            catch (HttpRequestException ex)
            {
                return $"Error: Unable to reach Ollama at {_endpoint}. {ex.Message}";
            }
            catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                return $"Error: Ollama request canceled. {ex.Message}";
            }
            catch (TaskCanceledException ex)
            {
                return $"Error: Ollama request timed out. {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"Error: Ollama request failed. {ex.Message}";
            }
        }

        /// <summary>
        /// Check if the LLM is available.
        /// </summary>
        public async Task<bool> IsAvailable(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_endpoint}/api/tags", cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private static string ResolveKeepAlive()
        {
            var raw = Environment.GetEnvironmentVariable("LOCALCURSOR_OLLAMA_KEEP_ALIVE");
            return string.IsNullOrWhiteSpace(raw) ? "0s" : raw.Trim();
        }
    }
}
