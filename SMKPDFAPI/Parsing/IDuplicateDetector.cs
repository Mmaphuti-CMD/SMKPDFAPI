using SMKPDFAPI.Models;

namespace SMKPDFAPI.Parsing;

public interface IDuplicateDetector
{
    List<Transaction> RemoveDuplicates(List<Transaction> transactions);
    List<Transaction> GetDuplicates(List<Transaction> transactions);
    
    /// <summary>
    /// Enhanced duplicate detection with hash IDs and metadata
    /// </summary>
    List<Transaction> DetectAndMarkDuplicates(List<Transaction> transactions);
    
    /// <summary>
    /// Get duplicate detection report with groups
    /// </summary>
    DuplicateDetectionReport GetDuplicateReport(List<Transaction> transactions);
}

