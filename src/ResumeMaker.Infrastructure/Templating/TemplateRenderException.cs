namespace ResumeMaker.Infrastructure.Templating;

/// <summary>Thrown when a template cannot be parsed or rendered (syntax error, limit exceeded...).</summary>
public sealed class TemplateRenderException : Exception
{
    public TemplateRenderException(string message, IReadOnlyList<string>? errors = null, Exception? inner = null)
        : base(message, inner)
    {
        Errors = errors ?? [];
    }

    public IReadOnlyList<string> Errors { get; }
}
