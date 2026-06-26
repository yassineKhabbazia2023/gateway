namespace ApiGateway.ProspectExperience.Constants;

/// <summary>
/// ProblemDetails extension keys and default values.
/// </summary>
public static class ProblemDetailsKeys
{
    /// <summary>
    /// Key for the field extension in ProblemDetails.
    /// </summary>
    public const string Field = "field";

    /// <summary>
    /// Key for the errorCode extension in ProblemDetails.
    /// </summary>
    public const string ErrorCode = "errorCode";

    /// <summary>
    /// Default field value when not found.
    /// </summary>
    public const string DefaultField = "unknown";

    /// <summary>
    /// Default error code when not found.
    /// </summary>
    public const string DefaultErrorCode = "VALIDATION_ERROR";

    /// <summary>
    /// Default error message for validation failures.
    /// </summary>
    public const string DefaultValidationMessage = "Validation failed";

    /// <summary>
    /// Default error message for file too large.
    /// </summary>
    public const string DefaultFileTooLargeMessage = "File size exceeds limit";

    /// <summary>
    /// Default error code for file too large.
    /// </summary>
    public const string DefaultFileTooLargeCode = "FILE_TOO_LARGE";
}
