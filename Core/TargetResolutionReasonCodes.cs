namespace LocalCursorAgent.Core;

public static class TargetResolutionReasonCodes
{
    public const string TargetSymbolNotFound = "TARGET_SYMBOL_NOT_FOUND";
    public const string TargetFileNotFound = "TARGET_FILE_NOT_FOUND";
    public const string TargetAmbiguous = "TARGET_AMBIGUOUS";
    public const string TargetLowConfidence = "TARGET_LOW_CONFIDENCE";
    public const string TargetNotApplicable = "TARGET_NOT_APPLICABLE";
    public const string TargetExactSymbolMatch = "TARGET_EXACT_SYMBOL_MATCH";
    public const string TargetExactFilenameMatch = "TARGET_EXACT_FILENAME_MATCH";
    public const string TargetPartialMatch = "TARGET_PARTIAL_MATCH";
    public const string TargetSemanticFallback = "TARGET_SEMANTIC_FALLBACK";
}
