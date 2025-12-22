namespace SMKPDFAPI.Models;

public record StatementMetadata(
    DateOnly? StatementDate = null,
    string? StatementNumber = null,
    int? TotalPages = null);

