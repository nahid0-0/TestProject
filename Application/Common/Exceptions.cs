namespace Chess.Application.Common;

public class NotFoundException(string message) : Exception(message);

public class ValidationException(string message, IReadOnlyDictionary<string, string[]>? errors = null) : Exception(message)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors ?? new Dictionary<string, string[]>();
}
