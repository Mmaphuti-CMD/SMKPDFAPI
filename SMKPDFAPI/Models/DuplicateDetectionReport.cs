namespace SMKPDFAPI.Models;

/// <summary>
/// Report of duplicate detection analysis
/// </summary>
public record DuplicateDetectionReport(
    int TotalTransactions,
    int UniqueTransactions,
    int DuplicateCount,
    List<DuplicateGroup> DuplicateGroups);

/// <summary>
/// Group of duplicate transactions
/// </summary>
public record DuplicateGroup(
    int GroupId,
    string TransactionHash,
    Transaction OriginalTransaction,
    List<Transaction> Duplicates);
