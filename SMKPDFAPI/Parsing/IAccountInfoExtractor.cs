using SMKPDFAPI.Models;

namespace SMKPDFAPI.Parsing;

public interface IAccountInfoExtractor
{
    AccountInfo ExtractAccountInfo(StatementText text, List<Transaction> transactions);
}

