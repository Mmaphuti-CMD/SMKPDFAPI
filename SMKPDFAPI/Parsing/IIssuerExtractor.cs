namespace SMKPDFAPI.Parsing;

public interface IIssuerExtractor
{
    string ExtractIssuer(StatementText text);
}

