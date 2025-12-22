using SMKPDFAPI.Models;

namespace SMKPDFAPI.Parsing;

public interface IStatementMetadataExtractor
{
    StatementMetadata ExtractMetadata(StatementText text, int? pageCount = null);
}

