using System.Globalization;
using System.Text.RegularExpressions;
using SMKPDFAPI.Models;

namespace SMKPDFAPI.Parsing;

public class RegexTransactionParser : ITransactionParser
{
    // Pattern for bank statement format: Date Description [Category] [Money In] [Money Out] [Fee] Balance
    // Examples:
    // "01/11/2025 Payment Received: M Madiope Other Income 200.00 238.04"
    // "01/11/2025 Banking App External Payment: King Rental Income -195.00 -2.00 41.04"
    // "01/11/2025 Live Better Interest Sweep Transfer -0.16 91.50"
    // "08/12/2025 Rana General Trading P Witbank (Card 7938) Furniture & Appliances -1 000.00 520.06"
    private static readonly Regex LinePattern = new(
        @"^(?<date>\d{2}/\d{2}/\d{4})\s*(?<rest>.+)$",
        RegexOptions.Compiled);
    
    // Pattern to find all monetary values (with optional spaces in thousands)
    private static readonly Regex MoneyPattern = new(
        @"-?(?:\d{1,3}(?:\s+\d{3})*|\d+)\.\d{2}",
        RegexOptions.Compiled);

    public List<Transaction> Parse(StatementText text)
    {
        var results = new List<Transaction>();
        var inTransactionHistory = false;
        var foundHeader = false;

        for (int i = 0; i < text.Lines.Count; i++)
        {
            var line = text.Lines[i];

            // Check if we're entering the transaction history section
            if (line.Contains("Transaction History", StringComparison.OrdinalIgnoreCase))
            {
                inTransactionHistory = true;
                // Check if header is on the same line
                if (line.Contains("Date", StringComparison.OrdinalIgnoreCase) && 
                    (line.Contains("Description", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("Money", StringComparison.OrdinalIgnoreCase)))
                {
                    foundHeader = true;
                }
                else
                {
                    foundHeader = false;
                }
                continue;
            }

            // Look for the table header row (might be on separate line or same line)
            if (inTransactionHistory && !foundHeader && 
                (line.Contains("Date", StringComparison.OrdinalIgnoreCase) && 
                 (line.Contains("Description", StringComparison.OrdinalIgnoreCase) ||
                  line.Contains("Money", StringComparison.OrdinalIgnoreCase))))
            {
                foundHeader = true;
                continue;
            }
            
            // If we're in transaction history but haven't found header yet, 
            // and this line starts with a date, assume we've passed the header
            if (inTransactionHistory && !foundHeader && LinePattern.IsMatch(line))
            {
                foundHeader = true; // Assume header was skipped/missing, proceed anyway
            }

            // Skip other header/footer lines
            if (line.Contains("Includes VAT", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Page", StringComparison.OrdinalIgnoreCase) && line.Contains("of", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // Only process lines after we've found the transaction history header
            if (!inTransactionHistory || !foundHeader)
            {
                continue;
            }

            var match = LinePattern.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var dateStr = match.Groups["date"].Value;
            var restOfLine = match.Groups["rest"].Value;

            // Find all monetary values in the line
            var moneyMatches = MoneyPattern.Matches(restOfLine);
            if (moneyMatches.Count == 0)
            {
                continue; // No amounts found, skip this line
            }

            // The last monetary value is always the balance
            var balanceMatch = moneyMatches[moneyMatches.Count - 1];
            var balanceStr = balanceMatch.Value.Replace(" ", ""); // Remove spaces from "1 000.00"
            var balance = decimal.Parse(balanceStr, NumberStyles.Any, CultureInfo.InvariantCulture);

            // Find the transaction amount (Money In or Money Out)
            // Look through all amounts except the last one (balance)
            decimal amount = 0;
            if (moneyMatches.Count > 1)
            {
                // There are amounts before the balance
                // Look for the first non-zero amount (could be Money In or Money Out)
                for (int j = 0; j < moneyMatches.Count - 1; j++)
                {
                    var amountStr = moneyMatches[j].Value.Replace(" ", "");
                    var amountValue = decimal.Parse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture);
                    
                    // Use the first non-zero amount we find
                    if (amountValue != 0)
                    {
                        amount = amountValue;
                        break;
                    }
                }
            }
            else if (moneyMatches.Count == 1)
            {
                // Only balance found, no transaction amount (might be a balance-only line)
                // Skip this line
                continue;
            }

            // Extract description - everything before the first monetary value
            // But we need to be careful - the first match might be the transaction amount or a fee
            // Look for the last monetary value before the balance (which is the transaction amount)
            var transactionAmountIndex = moneyMatches.Count > 1 
                ? restOfLine.LastIndexOf(moneyMatches[moneyMatches.Count - 2].Value, StringComparison.Ordinal)
                : restOfLine.IndexOf(moneyMatches[0].Value, StringComparison.Ordinal);
            
            var description = transactionAmountIndex > 0 
                ? restOfLine.Substring(0, transactionAmountIndex).Trim()
                : restOfLine.Trim();

            // Clean up description - remove trailing category words if they exist
            description = CleanDescription(description);

            try
            {
                var parsedDate = ParseDate(dateStr);
                // Capitec Bank is South African, use ZAR currency
                results.Add(new Transaction(parsedDate, description, amount, balance, "ZAR"));
            }
            catch
            {
                // Skip lines with invalid dates
                continue;
            }
        }

        return results;
    }

    private static string CleanDescription(string description)
    {
        // Remove common category words that might appear at the end
        var categories = new[] { 
            "Other Income", "Transfer", "Fees", "Rental Income", "Digital Payments", 
            "Furniture & Appliances", "Furniture &", "Cellphone", "Digital Subscriptions",
            "Interest", "New York Us", "New York", "New"
        };
        
        // Sort by length (longest first) to avoid partial matches
        var sortedCategories = categories.OrderByDescending(c => c.Length).ToArray();
        
        foreach (var category in sortedCategories)
        {
            if (description.EndsWith(category, StringComparison.OrdinalIgnoreCase))
            {
                description = description.Substring(0, description.Length - category.Length).TrimEnd();
            }
        }
        
        // Remove any trailing amounts that might have been attached (e.g., "Cellphone-8.00", "Rental Income-195.00")
        // Pattern: category name followed by a negative amount
        description = Regex.Replace(description, @"(Transfer|Cellphone|Digital Payments|Rental Income|Other Income|Fees|Interest|Digital Subscriptions|Furniture & Appliances)-\d+\.\d{2}$", "", RegexOptions.IgnoreCase);
        
        // Remove any trailing negative amounts that might be stuck to the description
        description = Regex.Replace(description, @"-\d+\.\d{2}$", "");
        
        // Remove any remaining category words that might be attached without spaces
        foreach (var category in sortedCategories)
        {
            if (description.EndsWith(category, StringComparison.OrdinalIgnoreCase))
            {
                description = description.Substring(0, description.Length - category.Length).TrimEnd();
            }
        }
        
        return description.Trim();
    }

    private static DateTime ParseDate(string value)
    {
        // Bank statement uses DD/MM/YYYY format
        var formats = new[]
        {
            "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy"
        };

        if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
        {
            return exact;
        }

        // Fallback to standard parsing
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        throw new FormatException($"Unable to parse date: {value}");
    }
}

