namespace SMKPDFAPI.Parsing;

public interface IStatementNormalizer
{
    StatementText Normalize(string raw);
}

