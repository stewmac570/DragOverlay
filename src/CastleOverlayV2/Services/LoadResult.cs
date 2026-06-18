namespace CastleOverlayV2.Services
{
    public enum ResultSeverity
    {
        None,
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Carries the outcome of a long-running load operation.
    /// Used by services that previously called <see cref="System.Windows.Forms.MessageBox.Show(string)"/> directly —
    /// services now return a result and the UI thread decides how to surface any message.
    /// </summary>
    public sealed record LoadResult<T>
    {
        public bool Ok { get; init; }
        public T? Value { get; init; }
        public string? Title { get; init; }
        public string? Message { get; init; }
        public ResultSeverity Severity { get; init; }

        public bool HasMessage => Title != null && Message != null;

        public static LoadResult<T> Success(T value) =>
            new() { Ok = true, Value = value, Severity = ResultSeverity.None };

        public static LoadResult<T> SuccessWithWarning(T value, string title, string message) =>
            new() { Ok = true, Value = value, Title = title, Message = message, Severity = ResultSeverity.Warning };

        public static LoadResult<T> Error(string title, string message) =>
            new() { Ok = false, Title = title, Message = message, Severity = ResultSeverity.Error };

        public static LoadResult<T> Info(string title, string message) =>
            new() { Ok = false, Title = title, Message = message, Severity = ResultSeverity.Info };
    }
}
