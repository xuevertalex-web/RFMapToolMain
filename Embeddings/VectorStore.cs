namespace LocalCursorAgent.Embeddings
{
    /// <summary>
    /// Stores embeddings and performs similarity search.
    /// Prevents duplicate indexing and enables efficient similarity searches.
    /// </summary>
    public class VectorStore
    {
        private readonly Dictionary<string, float[]> _vectors = new();
        private readonly Dictionary<string, string> _metadata = new();

        /// <summary>
        /// Add a vector to the store. Prevents duplicate indexing.
        /// </summary>
        public bool AddVector(string identifier, float[] embedding, string metadata = "")
        {
            if (string.IsNullOrWhiteSpace(identifier) || embedding == null)
                return false;

            // Check if already indexed (deduplication)
            if (_vectors.ContainsKey(identifier))
                return false; // Already indexed

            _vectors[identifier] = embedding;
            _metadata[identifier] = metadata;
            return true;
        }

        /// <summary>
        /// Check if a vector is already indexed.
        /// </summary>
        public bool IsIndexed(string identifier)
        {
            return _vectors.ContainsKey(identifier);
        }

        /// <summary>
        /// Get a vector by identifier.
        /// </summary>
        public float[]? GetVector(string identifier)
        {
            return _vectors.TryGetValue(identifier, out var vector) ? vector : null;
        }

        /// <summary>
        /// Find top-K most similar vectors using cosine similarity.
        /// Results sorted by similarity descending, deterministic order.
        /// </summary>
        public List<(string identifier, float similarity, string metadata)> FindSimilar(float[] queryVector, int topK = 5)
        {
            if (queryVector == null || queryVector.Length == 0)
                return new List<(string, float, string)>();

            // Ensure topK is between 5-15 for production stability
            topK = Math.Max(5, Math.Min(topK, 15));

            var similarities = new List<(string identifier, float similarity)>();

            foreach (var kvp in _vectors)
            {
                var similarity = CosineSimilarity(queryVector, kvp.Value);
                similarities.Add((kvp.Key, similarity));
            }

            // Sort by similarity descending, then by identifier for deterministic ordering
            var topMatches = similarities
                .OrderByDescending(x => x.similarity)
                .ThenBy(x => x.identifier) // Deterministic secondary sort
                .Take(topK)
                .ToList();

            var results = topMatches
                .Select(match => (match.identifier, match.similarity, _metadata.TryGetValue(match.identifier, out var meta) ? meta : ""))
                .ToList();

            return results;
        }

        /// <summary>
        /// Calculate cosine similarity between two vectors.
        /// </summary>
        private float CosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA.Length != vectorB.Length)
                return 0f;

            float dotProduct = 0f;
            float magnitudeA = 0f;
            float magnitudeB = 0f;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                magnitudeA += vectorA[i] * vectorA[i];
                magnitudeB += vectorB[i] * vectorB[i];
            }

            magnitudeA = (float)Math.Sqrt(magnitudeA);
            magnitudeB = (float)Math.Sqrt(magnitudeB);

            if (magnitudeA == 0f || magnitudeB == 0f)
                return 0f;

            return dotProduct / (magnitudeA * magnitudeB);
        }

        /// <summary>
        /// Get all identifiers in the store.
        /// </summary>
        public IEnumerable<string> GetAllIdentifiers()
        {
            return _vectors.Keys.OrderBy(x => x); // Deterministic order
        }

        /// <summary>
        /// Get size of vector store.
        /// </summary>
        public int Count => _vectors.Count;

        /// <summary>
        /// Clear all vectors.
        /// </summary>
        public void Clear()
        {
            _vectors.Clear();
            _metadata.Clear();
        }

        /// <summary>
        /// Get metadata for identifier.
        /// </summary>
        public string? GetMetadata(string identifier)
        {
            return _metadata.TryGetValue(identifier, out var meta) ? meta : null;
        }

        /// <summary>
        /// Remove vector from store by identifier.
        /// </summary>
        public bool RemoveVector(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return false;
            
            bool removedFromVectors = _vectors.Remove(identifier);
            bool removedFromMetadata = _metadata.Remove(identifier);
            
            return removedFromVectors || removedFromMetadata;
        }

        /// <summary>
        /// Update existing vector and metadata.
        /// </summary>
        public bool UpdateVector(string identifier, float[] newEmbedding, string? newMetadata = null)
        {
            if (string.IsNullOrWhiteSpace(identifier) || newEmbedding == null)
                return false;

            if (!_vectors.ContainsKey(identifier))
                return false;

            _vectors[identifier] = newEmbedding;
            
            if (newMetadata != null)
                _metadata[identifier] = newMetadata;

            return true;
        }

        /// <summary>
        /// Find similar vectors above minimum similarity threshold.
        /// </summary>
        public List<(string identifier, float similarity, string metadata)> FindSimilarAboveThreshold(float[] queryVector, float minSimilarity, int maxResults = 15)
        {
            if (queryVector == null || queryVector.Length == 0 || minSimilarity < -1f || minSimilarity > 1f)
                return new List<(string, float, string)>();

            var results = FindSimilar(queryVector, maxResults);
            return results.Where(x => x.similarity >= minSimilarity).ToList();
        }

        /// <summary>
        /// Filter vectors by metadata content.
        /// </summary>
        public List<(string identifier, string metadata)> FilterByMetadata(Func<string, bool> metadataPredicate)
        {
            if (metadataPredicate == null)
                return new List<(string, string)>();

            return _metadata
                .Where(kvp => metadataPredicate(kvp.Value))
                .Select(kvp => (kvp.Key, kvp.Value))
                .OrderBy(x => x.Key)
                .ToList();
        }

        /// <summary>
        /// Get store statistics summary.
        /// </summary>
        public (int totalVectors, int vectorsWithMetadata, float averageVectorDimension) GetStatistics()
        {
            int totalVectors = _vectors.Count;
            int vectorsWithMetadata = _metadata.Count(kvp => !string.IsNullOrEmpty(kvp.Value));
            float averageDimension = totalVectors > 0 
                ? (float)_vectors.Values.Average(v => v.Length) 
                : 0f;

            return (totalVectors, vectorsWithMetadata, averageDimension);
        }

        /// <summary>
        /// Save entire vector store to file system.
        /// </summary>
        public async Task SaveToFileAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentNullException(nameof(filePath));

            var data = new
            {
                Vectors = _vectors.ToDictionary(),
                Metadata = _metadata.ToDictionary(),
                SavedAt = DateTimeOffset.UtcNow
            };

            var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
        }

        /// <summary>
        /// Load vector store from file system.
        /// </summary>
        public static async Task<VectorStore> LoadFromFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
                return new VectorStore();

            var json = await File.ReadAllTextAsync(filePath);
            var store = new VectorStore();
            
            var data = System.Text.Json.JsonSerializer.Deserialize<VectorStoreData>(json);
            
            if (data != null)
            {
                foreach (var kvp in data.Vectors)
                    store._vectors[kvp.Key] = kvp.Value;
                    
                foreach (var kvp in data.Metadata)
                    store._metadata[kvp.Key] = kvp.Value;
            }

            return store;
        }

        private class VectorStoreData
        {
            public Dictionary<string, float[]> Vectors { get; set; } = new();
            public Dictionary<string, string> Metadata { get; set; } = new();
            public DateTimeOffset SavedAt { get; set; }
        }
    }
}
