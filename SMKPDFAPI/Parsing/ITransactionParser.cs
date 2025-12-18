using SMKPDFAPI.Models;

namespace SMKPDFAPI.Parsing;

public interface ITransactionParser
{
    List<Transaction> Parse(StatementText text);
}

