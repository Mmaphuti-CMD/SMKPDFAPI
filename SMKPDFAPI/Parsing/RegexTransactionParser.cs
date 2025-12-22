using System.Globalization;
using System.Text.RegularExpressions;
using SMKPDFAPI.Models;

namespace SMKPDFAPI.Parsing;

public class RegexTransactionParser : ITransactionParser
{
    // Pattern for bank statement format: Date | Description | Category | Money In | Money Out | Fee* | Balance
    // Column order: Date | Description | Category | Money In | Money Out | Fee* | Balance
    // Examples:
    // "01/11/2025 Payment Received: M Madiope Other Income 200.00 238.04" (Money In, Balance)
    // "01/11/2025 Banking App External Payment: King Rental Income -195.00 -2.00 41.04" (Money Out, Fee, Balance)
    // "01/11/2025 Live Better Interest Sweep Transfer -0.16 91.50" (Money Out, Balance)
    // "16/12/2025 Banking App External PayShap Payment: King Digital Payments -100.00 -6.00 43.56" (Money Out, Fee, Balance)
    // "08/12/2025 Rana General Trading P Witbank (Card 7938) Furniture & Appliances -1 000.00 520.06" (Money Out, Balance)
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

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // Check if line starts with a date pattern FIRST - this takes priority
            // This ensures we process transaction lines even if they appear in unexpected places
            var match = LinePattern.Match(line);
            bool isTransactionLine = match.Success;

            // Skip header/footer lines, but ONLY if they don't start with a date
            // This ensures transaction lines are never skipped
            if (!isTransactionLine)
            {
                if (line.Contains("Includes VAT", StringComparison.OrdinalIgnoreCase) ||
                    (line.Contains("Page", StringComparison.OrdinalIgnoreCase) && line.Contains("of", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
            }

            // Only process lines after we've found the transaction history header
            // BUT: if a line starts with a date, process it anyway (might be a transaction)
            if (!inTransactionHistory || !foundHeader)
            {
                // If line starts with date, assume we're in transaction section
                if (isTransactionLine)
                {
                    inTransactionHistory = true;
                    foundHeader = true; // Assume header was found
                }
                else
                {
                    continue;
                }
            }

            // If line doesn't match date pattern, skip it
            if (!isTransactionLine)
            {
                continue;
            }

            var dateStr = match.Groups["date"].Value;
            var restOfLine = match.Groups["rest"].Value.Trim();
            
            // Defensive check: ensure restOfLine is not empty
            if (string.IsNullOrWhiteSpace(restOfLine))
            {
                continue; // Skip lines with only a date
            }
            
            // MULTI-LINE TRANSACTION HANDLING: Merge continuation lines
            // If the next line doesn't start with a date and the current line doesn't have enough monetary values,
            // it might be a continuation of the description
            while (i + 1 < text.Lines.Count)
            {
                var nextLine = text.Lines[i + 1].Trim();
                
                // If next line starts with a date, it's a new transaction
                if (LinePattern.IsMatch(nextLine))
                {
                    break;
                }
                
                // If next line is empty or a header/footer, stop merging
                if (string.IsNullOrWhiteSpace(nextLine) ||
                    nextLine.Contains("Includes VAT", StringComparison.OrdinalIgnoreCase) ||
                    (nextLine.Contains("Page", StringComparison.OrdinalIgnoreCase) && nextLine.Contains("of", StringComparison.OrdinalIgnoreCase)))
                {
                    break;
                }
                
                // Check if current line has enough monetary values (at least 2: amount + balance)
                var currentMoneyMatches = MoneyPattern.Matches(restOfLine);
                if (currentMoneyMatches.Count >= 2)
                {
                    // Current line already has amount and balance, so it's complete
                    break;
                }
                
                // Merge the next line into the current line
                restOfLine = restOfLine + " " + nextLine;
                i++; // Skip the merged line
            }

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

            // Find the transaction amount (Money In or Money Out) and fee
            // Column order: Date | Description | Category | Money In | Money Out | Fee* | Balance
            // Pattern: [Money In OR Money Out] [Fee?] [Balance]
            // Examples:
            //   "-100.00 -6.00 43.56" (Money Out, Fee, Balance) - 3 values
            //   "-195.00 -2.00 41.04" (Money Out, Fee, Balance) - 3 values
            //   "200.00 238.04" (Money In, Balance) - 2 values (no fee)
            //   "-0.16 91.50" (Money Out, Balance) - 2 values (no fee)
            
            decimal amount = 0;
            decimal? fee = null;
            
            if (moneyMatches.Count >= 3)
            {
                // We have 3+ values: [Money In OR Money Out] [Fee?] [Balance]
                // Column structure: Money In/Money Out | Fee* | Balance
                // Parse all monetary values (excluding balance which is always last)
                var moneyInOrOutStr = moneyMatches[0].Value.Replace(" ", "");
                amount = decimal.Parse(moneyInOrOutStr, NumberStyles.Any, CultureInfo.InvariantCulture);
                
                // Check all values between first and last (excluding balance) to find the fee
                // Fee is typically the second-to-last value, but verify it's actually a fee
                for (int j = 1; j < moneyMatches.Count - 1; j++) // Skip first (amount) and last (balance)
                {
                    var potentialFeeStr = moneyMatches[j].Value.Replace(" ", "");
                    var potentialFeeValue = decimal.Parse(potentialFeeStr, NumberStyles.Any, CultureInfo.InvariantCulture);
                    
                    // Fee characteristics:
                    // 1. Always negative
                    // 2. Small absolute value (< 100, typically < 10)
                    // 3. Not zero
                    if (potentialFeeValue < 0 && Math.Abs(potentialFeeValue) < 100 && Math.Abs(potentialFeeValue) > 0.01m)
                    {
                        fee = Math.Abs(potentialFeeValue); // Store fee as positive value
                        break; // Found the fee, stop looking
                    }
                }
            }
            else if (moneyMatches.Count == 2)
            {
                // Two values: [Money In OR Money Out] [Balance] (no fee)
                // Column structure: Money In/Money Out | Balance
                var amountStr = moneyMatches[0].Value.Replace(" ", "");
                amount = decimal.Parse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture);
                // No fee in this case
            }
            else if (moneyMatches.Count == 1)
            {
                // Only balance found, no transaction amount
                // This is an incomplete transaction line - skip it
                continue;
            }

            // Extract description and category
            // Column structure: Date | Description | Category | Money In | Money Out | Fee* | Balance
            // Strategy: Use regex to extract everything before the first monetary value pattern
            // This is more reliable than string index operations
            
            string rawDescription;
            
            // Build a pattern to match: [description text] [monetary values...]
            // The description is everything before the first monetary value
            var firstMoneyMatch = MoneyPattern.Match(restOfLine);
            
            if (firstMoneyMatch.Success)
            {
                // Extract everything before the first monetary value
                rawDescription = restOfLine.Substring(0, firstMoneyMatch.Index).Trim();
            }
            else
            {
                // Fallback: This shouldn't happen since we already found moneyMatches
                // But be defensive - remove all monetary values from the line
                rawDescription = restOfLine.Trim();
                foreach (Match moneyMatch in moneyMatches)
                {
                    rawDescription = rawDescription.Replace(moneyMatch.Value, "").Trim();
                }
                rawDescription = Regex.Replace(rawDescription, @"\s+", " ").Trim();
            }
            
            // Ensure we have a description
            if (string.IsNullOrWhiteSpace(rawDescription))
            {
                rawDescription = "Transaction";
            }

            // Extract category before cleaning description
            string? category = ExtractCategory(rawDescription);
            
            // Clean up description - remove trailing category words if they exist
            var description = CleanDescription(rawDescription);

            // CRITICAL VALIDATION: Only skip if we have NO amount AND NO description
            // This ensures we capture all valid transactions, even if amount is 0 (adjustments)
            // The transaction "16/12/2025 Banking App External PayShap Payment: King Digital Payments -100.00 -6.00 43.56"
            // should always be captured if it has an amount
            if (amount == 0 && string.IsNullOrWhiteSpace(rawDescription))
            {
                // Both amount and description are missing - this is an incomplete transaction
                continue;
            }
            
            // FILTER OUT INVALID TRANSACTIONS: Check for header/footer text that got parsed as transactions
            // These typically contain patterns like "Page of", "Fee Summary", "Available Balance", etc.
            var invalidPatterns = new[]
            {
                "Page of",
                "Fee Summary",
                "Available Balance",
                "Tax Invoice",
                "VAT Registration",
                "Spending Summary",
                "Interest, Rewards and Fees"
            };
            
            bool isInvalidTransaction = invalidPatterns.Any(pattern => 
                rawDescription.Contains(pattern, StringComparison.OrdinalIgnoreCase));
            
            if (isInvalidTransaction)
            {
                // This is header/footer text, not a real transaction - skip it
                continue;
            }
            
            // If amount is 0 but we have a description, it might be a valid transaction (e.g., adjustments, reversals)
            // If amount is non-zero, we definitely have a valid transaction regardless of description

            // Determine transaction type
            var transactionType = DetermineTransactionType(amount, description);

            try
            {
                var parsedDate = ParseDate(dateStr);
                
                // Additional validation: ensure description is not empty
                if (string.IsNullOrWhiteSpace(description))
                {
                    description = "Transaction"; // Default description if empty
                }
                
                // Capitec Bank is South African, use ZAR currency
                results.Add(new Transaction(parsedDate, description, amount, balance, "ZAR", category, fee, transactionType));
            }
            catch (FormatException)
            {
                // Date parsing failed - skip this line
                // This could happen if the date format is unexpected
                continue;
            }
            catch (Exception)
            {
                // Other parsing errors - skip this line but log for debugging
                // This should rarely happen, but we want to be defensive
                continue;
            }
        }

        return results;
    }

    private static string? ExtractCategory(string description)
    {
        // Known categories from bank statements
        var categories = new[] { 
            "Other Income", "Transfer", "Fees", "Rental Income", "Digital Payments", 
            "Furniture & Appliances", "Cellphone", "Digital Subscriptions",
            "Interest", "Sweep Transfer"
        };
        
        // Sort by length (longest first) to avoid partial matches
        var sortedCategories = categories.OrderByDescending(c => c.Length).ToArray();
        
        foreach (var category in sortedCategories)
        {
            if (description.Contains(category, StringComparison.OrdinalIgnoreCase))
            {
                return category;
            }
        }
        
        return null;
    }

    private static string DetermineTransactionType(decimal amount, string description)
    {
        // Determine based on amount sign
        if (amount > 0)
        {
            return "Credit";
        }
        else if (amount < 0)
        {
            // Check description for transfer indicators
            if (description.Contains("Transfer", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("Sweep", StringComparison.OrdinalIgnoreCase))
            {
                return "Transfer";
            }
            return "Debit";
        }
        
        return "Unknown";
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

