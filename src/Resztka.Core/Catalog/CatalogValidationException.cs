namespace Resztka.Core.Catalog;

public sealed class CatalogValidationException(IReadOnlyList<string> errors)
    : Exception("Catalog is invalid:" + Environment.NewLine + string.Join(Environment.NewLine, errors.Select(e => " - " + e)))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
