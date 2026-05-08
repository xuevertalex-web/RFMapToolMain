namespace LocalCursorAgent.Core
{
    public partial class Agent
    {
        private IterationToolingResult HandleNoToolCallIterationResult(
            string task,
            string currentResponse,
            string? requestedNewFile,
            string? lastDeniedToolResult,
            string? lastBuildErrorSignature,
            string? lastBuildFailureCode)
        {
            var noToolDecision = HandleNoToolCallResponse(task, currentResponse, requestedNewFile);
            if (noToolDecision.IsHandled)
            {
                if (noToolDecision.ShouldContinue)
                {
                    return new IterationToolingResult
                    {
                        NextResponse = noToolDecision.Payload,
                        ShouldContinue = true,
                        PatchStarted = false,
                        BuildStarted = false,
                        LastDeniedToolResult = lastDeniedToolResult,
                        LastBuildErrorSignature = lastBuildErrorSignature,
                        LastBuildFailureCode = lastBuildFailureCode
                    };
                }

                return new IterationToolingResult
                {
                    NextResponse = currentResponse,
                    ShouldContinue = false,
                    FinalResult = noToolDecision.Payload,
                    PatchStarted = false,
                    BuildStarted = false,
                    LastDeniedToolResult = lastDeniedToolResult,
                    LastBuildErrorSignature = lastBuildErrorSignature,
                    LastBuildFailureCode = lastBuildFailureCode
                };
            }

            return new IterationToolingResult
            {
                NextResponse = currentResponse,
                ShouldContinue = false,
                PatchStarted = false,
                BuildStarted = false,
                LastDeniedToolResult = lastDeniedToolResult,
                LastBuildErrorSignature = lastBuildErrorSignature,
                LastBuildFailureCode = lastBuildFailureCode
            };
        }

        private IterationToolingResult? HandleEmptyToolCallIterationResult(
            string task,
            bool analysisOnlyTask,
            string currentResponse,
            string? lastDeniedToolResult,
            string? lastBuildErrorSignature,
            string? lastBuildFailureCode)
        {
            var emptyToolDecision = HandleEmptyParsedToolCalls(task, analysisOnlyTask, currentResponse);
            if (!emptyToolDecision.IsHandled)
                return null;

            if (emptyToolDecision.ShouldContinue)
            {
                return new IterationToolingResult
                {
                    NextResponse = emptyToolDecision.Payload,
                    ShouldContinue = true,
                    PatchStarted = false,
                    BuildStarted = false,
                    LastDeniedToolResult = lastDeniedToolResult,
                    LastBuildErrorSignature = lastBuildErrorSignature,
                    LastBuildFailureCode = lastBuildFailureCode,
                    LastSuccessfulStep = "ToolCallsParsed",
                    LastKnownAction = "Parsed 0 tool calls"
                };
            }

            return new IterationToolingResult
            {
                NextResponse = currentResponse,
                ShouldContinue = false,
                FinalResult = emptyToolDecision.Payload,
                PatchStarted = false,
                BuildStarted = false,
                LastDeniedToolResult = lastDeniedToolResult,
                LastBuildErrorSignature = lastBuildErrorSignature,
                LastBuildFailureCode = lastBuildFailureCode,
                LastSuccessfulStep = "ToolCallsParsed",
                LastKnownAction = "Parsed 0 tool calls"
            };
        }
    }
}
