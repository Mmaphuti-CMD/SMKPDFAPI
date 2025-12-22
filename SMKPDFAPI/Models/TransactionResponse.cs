namespace SMKPDFAPI.Models;

public record TransactionResponse(
    string Issuer, 
    DateOnly PeriodStart, 
    DateOnly PeriodEnd, 
    string Duration,
    int TransactionCount,
    AccountInfo? AccountInfo = null,
    StatementMetadata? Metadata = null,
    List<Transaction> Transactions = null!);

