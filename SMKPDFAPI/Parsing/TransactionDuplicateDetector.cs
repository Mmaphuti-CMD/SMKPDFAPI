using System.Security.Cryptography;
using System.Text;
using SMKPDFAPI.Models;

namespace SMKPDFAPI.Parsing;

public class TransactionDuplicateDetector : IDuplicateDetector
{
    // Tolerance for amount differences (in currency units)
    // Transactions within this tolerance are considered potential duplicates
    private const decimal AmountTolerance = 0.01m;
    public List<Transaction> RemoveDuplicates(List<Transaction> transactions)
    {
        if (transactions == null || transactions.Count == 0)
            return transactions ?? new List<Transaction>();

        // Group by key characteristics: Date, Description, and Amount
        // Transactions with same date, description, and amount are considered duplicates
        var seen = new HashSet<string>();
        var unique = new List<Transaction>();

        foreach (var transaction in transactions)
        {
            var key = GenerateKey(transaction);
            
            if (!seen.Contains(key))
            {
                seen.Add(key);
                unique.Add(transaction);
            }
        }

        return unique;
    }

    public List<Transaction> GetDuplicates(List<Transaction> transactions)
    {
        if (transactions == null || transactions.Count == 0)
            return new List<Transaction>();

        var seen = new Dictionary<string, Transaction>();
        var duplicates = new List<Transaction>();

        foreach (var transaction in transactions)
        {
            var key = GenerateKey(transaction);
            
            if (seen.ContainsKey(key))
            {
                // This is a duplicate
                if (!duplicates.Contains(seen[key]))
                {
                    duplicates.Add(seen[key]); // Add the first occurrence
                }
                duplicates.Add(transaction); // Add this duplicate
            }
            else
            {
                seen[key] = transaction;
            }
        }

        return duplicates;
    }

    private static string GenerateKey(Transaction transaction)
    {
        // Normalize description: remove extra spaces, convert to lowercase
        var normalizedDesc = System.Text.RegularExpressions.Regex.Replace(
            transaction.Description.ToLowerInvariant(), 
            @"\s+", 
            " ").Trim();
        
        // Create key from date (date only, no time), normalized description, and amount
        var dateKey = transaction.Date.ToString("yyyy-MM-dd");
        var amountKey = transaction.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        
        return $"{dateKey}|{normalizedDesc}|{amountKey}";
    }

    /// <summary>
    /// Enhanced duplicate detection that marks transactions with hash IDs and duplicate flags
    /// </summary>
    public List<Transaction> DetectAndMarkDuplicates(List<Transaction> transactions)
    {
        if (transactions == null || transactions.Count == 0)
            return transactions ?? new List<Transaction>();

        // First, generate hashes for all transactions
        var transactionsWithHashes = transactions.Select(t => t with
        {
            TransactionHash = GenerateTransactionHash(t)
        }).ToList();

        // Group by hash to find duplicates
        var hashGroups = transactionsWithHashes
            .GroupBy(t => t.TransactionHash)
            .ToList();

        var result = new List<Transaction>();

        foreach (var group in hashGroups)
        {
            var groupList = group.ToList();
            
            if (groupList.Count == 1)
            {
                // Unique transaction
                result.Add(groupList[0] with { IsDuplicate = false });
            }
            else
            {
                // Found duplicates - mark first as original, rest as duplicates
                var original = groupList[0];
                result.Add(original with 
                { 
                    IsDuplicate = false,
                    OriginalTransactionHash = null
                });

                // Mark all others as duplicates
                foreach (var duplicate in groupList.Skip(1))
                {
                    result.Add(duplicate with
                    {
                        IsDuplicate = true,
                        OriginalTransactionHash = original.TransactionHash
                    });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Get detailed duplicate detection report with groups
    /// </summary>
    public DuplicateDetectionReport GetDuplicateReport(List<Transaction> transactions)
    {
        if (transactions == null || transactions.Count == 0)
        {
            return new DuplicateDetectionReport(0, 0, 0, new List<DuplicateGroup>());
        }

        var markedTransactions = DetectAndMarkDuplicates(transactions);
        
        // Group duplicates together
        var duplicateGroups = new List<DuplicateGroup>();
        var processedHashes = new HashSet<string>();
        int groupId = 1;

        foreach (var transaction in markedTransactions)
        {
            if (transaction.IsDuplicate && !processedHashes.Contains(transaction.TransactionHash!))
            {
                // Find all transactions with this hash (original + duplicates)
                var group = markedTransactions
                    .Where(t => t.TransactionHash == transaction.OriginalTransactionHash || 
                               t.TransactionHash == transaction.TransactionHash)
                    .ToList();

                if (group.Count > 1)
                {
                    var original = group.First(t => !t.IsDuplicate);
                    var duplicates = group.Where(t => t.IsDuplicate).ToList();

                    duplicateGroups.Add(new DuplicateGroup(
                        GroupId: groupId++,
                        TransactionHash: original.TransactionHash!,
                        OriginalTransaction: original,
                        Duplicates: duplicates
                    ));

                    processedHashes.Add(transaction.TransactionHash!);
                }
            }
        }

        var uniqueCount = markedTransactions.Count(t => !t.IsDuplicate);
        var duplicateCount = markedTransactions.Count(t => t.IsDuplicate);

        return new DuplicateDetectionReport(
            TotalTransactions: transactions.Count,
            UniqueTransactions: uniqueCount,
            DuplicateCount: duplicateCount,
            DuplicateGroups: duplicateGroups
        );
    }

    /// <summary>
    /// Generate a unique hash/ID for a transaction based on key fields
    /// Uses SHA256 to create a deterministic hash
    /// </summary>
    private static string GenerateTransactionHash(Transaction transaction)
    {
        // Create hash input from key transaction fields
        // Include: Date, Description, Amount, Category, Fee, TransactionType
        var hashInput = $"{transaction.Date:yyyy-MM-dd}|" +
                       $"{NormalizeDescription(transaction.Description)}|" +
                       $"{transaction.Amount:F2}|" +
                       $"{transaction.Category ?? ""}|" +
                       $"{transaction.Fee?.ToString("F2") ?? ""}|" +
                       $"{transaction.TransactionType}";

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(hashInput));
        return Convert.ToHexString(hashBytes).Substring(0, 16); // Use first 16 chars as short hash
    }

    private static string NormalizeDescription(string description)
    {
        // Normalize description for consistent hashing
        // Remove extra spaces, convert to lowercase, trim
        return System.Text.RegularExpressions.Regex.Replace(
            description.ToLowerInvariant(),
            @"\s+",
            " ").Trim();
    }

    /// <summary>
    /// More sophisticated duplicate matching with fuzzy logic
    /// Checks for near-duplicates (same date, similar description, similar amount)
    /// </summary>
    private static bool AreSimilarTransactions(Transaction t1, Transaction t2)
    {
        // Same date
        if (t1.Date.Date != t2.Date.Date)
            return false;

        // Similar amount (within tolerance)
        if (Math.Abs(t1.Amount - t2.Amount) > AmountTolerance)
            return false;

        // Similar description (normalized comparison)
        var desc1 = NormalizeDescription(t1.Description);
        var desc2 = NormalizeDescription(t2.Description);
        
        // Exact match or very similar (one contains the other)
        if (desc1 == desc2 || desc1.Contains(desc2) || desc2.Contains(desc1))
            return true;

        // Check if descriptions are similar (Levenshtein distance could be used here)
        // For now, check if they share significant words
        var words1 = desc1.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var words2 = desc2.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (words1.Length > 0 && words2.Length > 0)
        {
            var commonWords = words1.Intersect(words2, StringComparer.OrdinalIgnoreCase).Count();
            var minWords = Math.Min(words1.Length, words2.Length);
            
            // If more than 50% of words match, consider similar
            if (minWords > 0 && (double)commonWords / minWords > 0.5)
                return true;
        }

        return false;
    }
}

