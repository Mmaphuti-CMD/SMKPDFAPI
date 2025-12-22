namespace SMKPDFAPI.Models;

public record Transaction(
    DateTime Date, 
    string Description, 
    decimal Amount, 
    decimal? Balance = null, 
    string Currency = "ZAR",
    string? Category = null,
    decimal? Fee = null,
    string TransactionType = "Unknown", // "Credit", "Debit", "Transfer", "Unknown"
    string? TransactionHash = null, // Unique hash/ID for duplicate detection
    bool IsDuplicate = false, // Flag indicating if this is a duplicate
    string? OriginalTransactionHash = null); // Hash of the original transaction if this is a duplicate

