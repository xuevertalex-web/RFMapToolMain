namespace LocalCursorAgent.Diagnostics
{
    public partial class ExecutionTracer
    {
        #region Diagnostic Engine

        public DiagnosticSummary GenerateDiagnosticSummary()
        {
            var summary = new DiagnosticSummary
            {
                Timestamp = DateTime.UtcNow,
                TotalFilesConsidered = _fileTraces.Count,
                SelectedFiles = _fileTraces.Count(t => t.State == "Selected"),
                RejectedFiles = _fileTraces.Count(t => t.State == "Rejected"),
                MemoryInfluence = CalculateMemoryInfluence(),
                SymbolSystemEffectiveness = CalculateSymbolEffectiveness(),
                SemanticEffectiveness = CalculateSemanticEffectiveness(),
                ConfidenceScore = CalculateConfidenceScore()
            };

            summary.OptimalDecision = summary.ConfidenceScore > 0.7;
            summary.MemoryImpact = DetermineMemoryImpact();
            summary.SymbolImpact = DetermineSymbolImpact();
            summary.SemanticImpact = DetermineSemanticImpact();

            return summary;
        }

        private double CalculateMemoryInfluence()
        {
            if (_memoryInfluences.Count == 0)
                return 0;

            double totalInfluence = 0;
            foreach (var influence in _memoryInfluences)
            {
                totalInfluence += influence.TotalMemoryContribution;
            }

            return totalInfluence / _memoryInfluences.Count;
        }

        private double CalculateSymbolEffectiveness()
        {
            var symbolMatches = _fileTraces.Count(t => t.SymbolScore > 0.1);
            return (double)symbolMatches / Math.Max(_fileTraces.Count, 1);
        }

        private double CalculateSemanticEffectiveness()
        {
            var semanticMatches = _fileTraces.Count(t => t.SemanticScore > 0.1);
            return (double)semanticMatches / Math.Max(_fileTraces.Count, 1);
        }

        private double CalculateConfidenceScore()
        {
            var highScoreFiles = _fileTraces.Count(t => t.FinalScore > 0.8);
            return (double)highScoreFiles / Math.Max(_fileTraces.Count, 1);
        }

        private string DetermineMemoryImpact()
        {
            var avgMemory = CalculateMemoryInfluence();
            if (avgMemory > 0.3)
                return "High positive impact";
            if (avgMemory > 0.1)
                return "Moderate positive impact";
            if (avgMemory < -0.1)
                return "Negative impact";
            return "Neutral impact";
        }

        private string DetermineSymbolImpact()
        {
            var effectiveness = CalculateSymbolEffectiveness();
            if (effectiveness > 0.7)
                return "High positive impact";
            if (effectiveness > 0.3)
                return "Moderate positive impact";
            return "Low impact";
        }

        private string DetermineSemanticImpact()
        {
            var effectiveness = CalculateSemanticEffectiveness();
            if (effectiveness > 0.7)
                return "High positive impact";
            if (effectiveness > 0.3)
                return "Moderate positive impact";
            return "Low impact";
        }

        #endregion

        #region Recommendations Engine

        public List<string> GenerateRecommendations()
        {
            var recommendations = new List<string>();
            var summary = GenerateDiagnosticSummary();

            if (summary.MemoryInfluence > 0.3)
                recommendations.Add("Memory influence too strong, reduce weight to 0.15");

            if (summary.SymbolImpact == "Low impact")
                recommendations.Add("Symbol match underused, increase boost to +0.25");

            if (summary.SemanticImpact == "Low impact")
                recommendations.Add("Semantic retrieval selecting too many irrelevant files");

            if (summary.TotalFilesConsidered > 20)
                recommendations.Add("Context size too large, reduce max files to 10");

            if (summary.RejectedFiles > summary.SelectedFiles)
                recommendations.Add("Too many files rejected, consider relaxing filters");

            return recommendations;
        }

        #endregion
    }
}
