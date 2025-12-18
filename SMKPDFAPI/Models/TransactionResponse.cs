namespace SMKPDFAPI.Models;

public record TransactionResponse(string Issuer, DateOnly PeriodStart, DateOnly PeriodEnd, List<Transaction> Transactions);

