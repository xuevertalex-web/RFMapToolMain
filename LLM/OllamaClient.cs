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
        private string? _resolvedModel;

        public OllamaClient(string endpoint = "http://localhost:11434", string model = "qwen2.5-coder:3b-instruct-q4_K_M")
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
                var requestModel = await ResolveRequestModel(cancellationToken);
                var requestPayload = new
                {
                    model = requestModel,
                    prompt,
                    stream = false,
                    keep_alive = _keepAlive
                };

                var json = JsonSerializer.Serialize(requestPayload);
                using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_endpoint}/api/generate", content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                    return $"Error: LLM returned status {response.StatusCode} (model '{requestModel}' at '{_endpoint}')";

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

        private async Task<string> ResolveRequestModel(CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(_resolvedModel))
                return _resolvedModel;

            var requested = _model.Trim();
            if (string.IsNullOrWhiteSpace(requested))
            {
                _resolvedModel = "qwen2.5-coder:3b-instruct-q4_K_M";
                return _resolvedModel;
            }

            try
            {
                using var tagsCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                tagsCts.CancelAfter(TimeSpan.FromSeconds(2));
                var tagsResponse = await _httpClient.GetAsync($"{_endpoint}/api/tags", tagsCts.Token);
                if (!tagsResponse.IsSuccessStatusCode)
                {
                    _resolvedModel = requested;
                    return _resolvedModel;
                }

                var tagsBody = await tagsResponse.Content.ReadAsStringAsync();
                using var tagsJson = JsonDocument.Parse(tagsBody);
                if (!tagsJson.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
                {
                    _resolvedModel = requested;
                    return _resolvedModel;
                }

                var available = new List<string>();
                foreach (var item in models.EnumerateArray())
                {
                    if (item.TryGetProperty("name", out var nameElement))
                    {
                        var name = nameElement.GetString();
                        if (!string.IsNullOrWhiteSpace(name))
                            available.Add(name.Trim());
                    }
                }

                _resolvedModel = ResolveModelAlias(requested, available);
                return _resolvedModel;
            }
            catch
            {
                _resolvedModel = requested;
                return _resolvedModel;
            }
        }

        public static string ResolveModelAlias(string requestedModel, IReadOnlyList<string> availableModels)
        {
            var requested = string.IsNullOrWhiteSpace(requestedModel) ? "qwen2.5-coder:3b-instruct-q4_K_M" : requestedModel.Trim();
            if (availableModels is null || availableModels.Count == 0)
                return requested;

            var direct = availableModels.FirstOrDefault(x => string.Equals(x?.Trim(), requested, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(direct))
                return direct.Trim();

            if (requested.StartsWith("qwen2.5-coder:7b-instruct-q4_k_m", StringComparison.OrdinalIgnoreCase))
            {
                var matched = availableModels
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .FirstOrDefault(x =>
                        x.Contains("qwen2.5", StringComparison.OrdinalIgnoreCase) &&
                        x.Contains("coder", StringComparison.OrdinalIgnoreCase) &&
                        x.Contains("7b", StringComparison.OrdinalIgnoreCase) &&
                        x.Contains("instruct", StringComparison.OrdinalIgnoreCase) &&
                        x.Contains("q4_k_m", StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(matched))
                    return matched;
            }

            return requested;
        }

        private static string ResolveKeepAlive()
        {
            var raw = Environment.GetEnvironmentVariable("LOCALCURSOR_OLLAMA_KEEP_ALIVE");
            return string.IsNullOrWhiteSpace(raw) ? "0s" : raw.Trim();
        }
    }
}
