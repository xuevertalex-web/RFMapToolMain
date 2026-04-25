using System.Text.Json;

namespace LocalCursorAgent.Embeddings
{
    public enum EmbeddingRuntimeStatus
    {
        Enabled,
        Degraded,
        Disabled
    }

    /// <summary>
    /// Service for generating embeddings using Ollama.
    /// Gracefully handles transient failures and degrades without crashing indexing.
    /// </summary>
    public class EmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _endpoint;
        private readonly string _model;
        private readonly bool _disabledByConfiguration;
        private bool _serviceUnavailableLogged;
        private bool _disabledForSession;
        private int _consecutiveFailures;

        public EmbeddingService(string endpoint = "http://localhost:11434", string model = "nomic-embed-text", bool disabled = false)
        {
            _endpoint = endpoint;
            _model = model;
            _disabledByConfiguration = disabled;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        }

        /// <summary>
        /// Generate embedding for a text input.
        /// Returns null on failure to enable graceful degradation.
        /// </summary>
        public async Task<float[]?> GenerateEmbedding(string text)
        {
            if (_disabledByConfiguration || _disabledForSession)
                return null;

            if (string.IsNullOrWhiteSpace(text))
                return null;

            var requestPayload = new
            {
                model = _model,
                prompt = text
            };

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    var json = JsonSerializer.Serialize(requestPayload);
                    using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                    var response = await _httpClient.PostAsync($"{_endpoint}/api/embeddings", content);

                    if (!response.IsSuccessStatusCode)
                    {
                        if (ShouldRetry(response.StatusCode) && attempt < 3)
                        {
                            await Task.Delay(GetRetryDelay(attempt));
                            continue;
                        }

                        LogEmbeddingFailure($"Embedding service error: {response.StatusCode}");
                        return null;
                    }

                    var responseBody = await response.Content.ReadAsStringAsync();
                    using var jsonDoc = JsonDocument.Parse(responseBody);

                    if (jsonDoc.RootElement.TryGetProperty("embedding", out var embeddingElement))
                    {
                        _serviceUnavailableLogged = false;
                        _disabledForSession = false;
                        _consecutiveFailures = 0;
                        return embeddingElement.EnumerateArray()
                            .Select(e => e.GetSingle())
                            .ToArray();
                    }

                    LogEmbeddingFailure("Embedding response did not contain an embedding vector");
                    return null;
                }
                catch (HttpRequestException ex)
                {
                    if (attempt < 3)
                    {
                        await Task.Delay(GetRetryDelay(attempt));
                        continue;
                    }

                    LogEmbeddingFailure($"Connection error to embedding service: {ex.Message}");
                    return null;
                }
                catch (TaskCanceledException ex)
                {
                    if (attempt < 3)
                    {
                        await Task.Delay(GetRetryDelay(attempt));
                        continue;
                    }

                    LogEmbeddingFailure($"Embedding request timed out: {ex.Message}");
                    return null;
                }
                catch (Exception ex)
                {
                    LogEmbeddingFailure($"Embedding generation failed: {ex.Message}");
                    return null;
                }
            }

            return null;
        }

        /// <summary>
        /// Batch generate embeddings for multiple texts.
        /// Fails gracefully for individual text items.
        /// </summary>
        public async Task<Dictionary<string, float[]>> GenerateEmbeddingsBatch(IEnumerable<string> texts)
        {
            var results = new Dictionary<string, float[]>();

            foreach (var text in texts)
            {
                var embedding = await GenerateEmbedding(text);
                if (embedding != null)
                {
                    results[text] = embedding;
                }
            }

            return results;
        }

        /// <summary>
        /// Check if embedding model is available.
        /// </summary>
        public async Task<bool> IsAvailable()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_endpoint}/api/tags");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public EmbeddingRuntimeStatus Status
        {
            get
            {
                if (_disabledByConfiguration || _disabledForSession)
                    return EmbeddingRuntimeStatus.Disabled;

                if (_consecutiveFailures > 0)
                    return EmbeddingRuntimeStatus.Degraded;

                return EmbeddingRuntimeStatus.Enabled;
            }
        }

        public string DescribeStatus() => Status switch
        {
            EmbeddingRuntimeStatus.Enabled => "enabled",
            EmbeddingRuntimeStatus.Degraded => "degraded",
            EmbeddingRuntimeStatus.Disabled => "disabled",
            _ => "unknown"
        };

        private static bool ShouldRetry(System.Net.HttpStatusCode statusCode)
        {
            var numeric = (int)statusCode;
            return numeric >= 500 || statusCode == System.Net.HttpStatusCode.RequestTimeout;
        }

        private static TimeSpan GetRetryDelay(int attempt)
        {
            return TimeSpan.FromMilliseconds(250 * attempt);
        }

        private void LogEmbeddingFailure(string message)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= 3)
            {
                _disabledForSession = true;
            }

            if (_serviceUnavailableLogged)
                return;

            Console.WriteLine($"[embedding] {message}");
            if (_consecutiveFailures >= 3)
            {
                Console.WriteLine("[embedding] Too many embedding failures. Semantic embeddings are disabled for this session.");
            }
            else
            {
                Console.WriteLine("[embedding] Semantic indexing will continue in degraded mode.");
            }
            Console.WriteLine($"[embedding] Status: {DescribeStatus()}");
            _serviceUnavailableLogged = true;
        }
    }
}
