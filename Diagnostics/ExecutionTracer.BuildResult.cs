using System.Collections.Generic;
using LocalCursorAgent.Execution;

namespace LocalCursorAgent.Diagnostics
{
    public partial class ExecutionTracer
    {
        #region Build Result

        public void LogBuildVerificationResult(BuildVerifier.BuildResult result)
        {
            var classification = result.Success ? "Success" : (result.Errors.Count > 0 ? "CompilationError" : "Unknown");
            var rootCauseGuess = result.Success
                ? "No build issues"
                : (result.Errors.Count > 0 ? result.Errors[0] : "Build process failed");
            var fixReasoning = result.Success
                ? "Apply changes to source"
                : "Return errors to agent and continue iteration";

            _buildResults.Add(new BuildResult
            {
                Timestamp = DateTime.UtcNow,
                Success = result.Success,
                ErrorClassification = classification,
                RootCauseGuess = rootCauseGuess,
                FixAttemptReasoning = fixReasoning
            });

            LogEvent("BuildVerification", "Build result evaluated", new Dictionary<string, object>
            {
                { "Success", result.Success },
                { "ErrorCount", result.Errors.Count },
                { "WarningCount", result.Warnings.Count },
                { "ErrorClassification", classification },
                { "RootCauseGuess", rootCauseGuess },
                { "FixAttemptReasoning", fixReasoning },
                { "Errors", result.Errors.ToArray() },
                { "Warnings", result.Warnings.ToArray() }
            });
        }

        #endregion
    }
}
