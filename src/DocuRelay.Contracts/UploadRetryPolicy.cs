namespace DocuRelay.Contracts;

public static class UploadRetryPolicy
{
    public const int MaximumAttempts = 3;

    public static bool IsTerminalFailure(int failedAttemptCount) => failedAttemptCount >= MaximumAttempts;
}
