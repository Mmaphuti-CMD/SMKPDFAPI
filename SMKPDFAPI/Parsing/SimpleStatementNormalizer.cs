using System.Text.RegularExpressions;

namespace SMKPDFAPI.Parsing;

public class SimpleStatementNormalizer : IStatementNormalizer
{
    public StatementText Normalize(string raw)
    {
        // First, try to split by common line break patterns
        // PDFs might use \r\n, \n, \r, or no line breaks at all
        var lines = raw
            .Replace("\r\n", "\n")
            .Replace("\r", "\n")
            .Split('\n')
            .Select(l => Regex.Replace(l, @"\s{2,}", " ").Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            // Keep page markers (___PAGE_X___) but filter out standalone "Page X of Y" lines
            // IMPORTANT: Page markers must be preserved for page tracking
            .Where(l => l.StartsWith("___PAGE_", StringComparison.Ordinal) || !Regex.IsMatch(l, @"^Page\s+\d+\s+of\s+\d+$", RegexOptions.IgnoreCase))
            .ToList();

        // If we only got 1-2 lines, the PDF might not have proper line breaks
        // Try to split by date patterns (DD/MM/YYYY) to break up transactions
        // IMPORTANT: Preserve page markers (___PAGE_X___) - they must remain as separate lines
        if (lines.Count <= 2)
        {
            var splitLines = new List<string>();
            foreach (var line in lines)
            {
                // Preserve page markers as-is - don't split them
                if (line.StartsWith("___PAGE_", StringComparison.Ordinal))
                {
                    splitLines.Add(line);
                    continue;
                }
                
                // Check if this line contains "Transaction History" - this is where transactions start
                var transactionHistoryIndex = line.IndexOf("Transaction History", StringComparison.OrdinalIgnoreCase);
                if (transactionHistoryIndex >= 0)
                {
                    // Find where transactions actually start (after the header row)
                    var headerIndex = line.IndexOf("DateDescription", StringComparison.OrdinalIgnoreCase);
                    if (headerIndex < 0)
                    {
                        headerIndex = line.IndexOf("Money In Money Out", StringComparison.OrdinalIgnoreCase);
                    }
                    
                    // Get the part before Transaction History (keep as is)
                    if (transactionHistoryIndex > 0)
                    {
                        splitLines.Add(line.Substring(0, transactionHistoryIndex).Trim());
                    }
                    
                    // Add Transaction History header
                    splitLines.Add("Transaction History");
                    
                    // Get the transaction section (everything after the header)
                    var transactionStart = headerIndex >= 0 ? line.IndexOf("Balance", headerIndex, StringComparison.OrdinalIgnoreCase) + 7 : transactionHistoryIndex + 19;
                    if (transactionStart < 0) transactionStart = transactionHistoryIndex + 19;
                    
                    var transactionSection = line.Substring(transactionStart).Trim();
                    
                    // Split by date pattern (DD/MM/YYYY) - each transaction starts with a date
                    var datePattern = @"(?=\d{2}/\d{2}/\d{4})";
                    var transactionLines = Regex.Split(transactionSection, datePattern)
                        .Where(l => !string.IsNullOrWhiteSpace(l) && Regex.IsMatch(l, @"^\d{2}/\d{2}/\d{4}"))
                        .ToList();
                    
                    splitLines.AddRange(transactionLines);
                }
                else
                {
                    // No Transaction History, try to split by dates anyway
                    var datePattern = @"(?=\d{2}/\d{2}/\d{4})";
                    var dateSplitLines = Regex.Split(line, datePattern)
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .ToList();
                    
                    splitLines.AddRange(dateSplitLines);
                }
            }
            // Preserve page markers when filtering
            lines = splitLines.Where(l => !string.IsNullOrWhiteSpace(l) || l.StartsWith("___PAGE_", StringComparison.Ordinal)).ToList();
        }

        return new StatementText(lines);
    }
}

