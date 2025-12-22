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
            decimal? fee = null;
            
            if (moneyMatches.Count > 1)
            {
                // IMPROVED LOGIC: Handle transactions with fees more accurately
                // Pattern: [Amount] [Fee?] [Balance]
                // Examples:
                //   "-100.00 -6.00 43.56" (amount, fee, balance) - 3 values
                //   "-195.00 -2.00 41.04" (amount, fee, balance) - 3 values
                //   "200.00 238.04" (amount, balance) - 2 values
                
                if (moneyMatches.Count >= 3)
                {
                    // We have 3+ values: likely [Amount] [Fee] [Balance]
                    // The transaction amount is typically the first value (larger absolute value)
                    // The fee is typically the second-to-last (smaller, negative value)
                    
                    var firstAmountStr = moneyMatches[0].Value.Replace(" ", "");
                    var firstAmount = decimal.Parse(firstAmountStr, NumberStyles.Any, CultureInfo.InvariantCulture);
                    
                    var secondAmountStr = moneyMatches[moneyMatches.Count - 2].Value.Replace(" ", "");
                    var secondAmount = decimal.Parse(secondAmountStr, NumberStyles.Any, CultureInfo.InvariantCulture);
                    
                    // Determine which is the transaction amount and which is the fee
                    // Transaction amount is typically larger in absolute value
                    if (Math.Abs(firstAmount) >= Math.Abs(secondAmount))
                    {
                        // First is transaction amount, second is fee
                        amount = firstAmount;
                        // Fee is typically negative and small
                        if (secondAmount < 0 && Math.Abs(secondAmount) < 1000)
                        {
                            fee = Math.Abs(secondAmount);
                        }
                    }
                    else
                    {
                        // Second is transaction amount, first might be fee or part of description
                        amount = secondAmount;
                        if (firstAmount < 0 && Math.Abs(firstAmount) < 1000)
                        {
                            fee = Math.Abs(firstAmount);
                        }
                    }
                }
                else if (moneyMatches.Count == 2)
                {
                    // Two values: [Amount] [Balance]
                    var amountStr = moneyMatches[0].Value.Replace(" ", "");
                    amount = decimal.Parse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture);
                }
                
                // Fallback: If amount is still 0, try to find any non-zero amount
                if (amount == 0)
                {
                    for (int j = 0; j < moneyMatches.Count - 1; j++)
                    {
                        var amountStr = moneyMatches[j].Value.Replace(" ", "");
                        var amountValue = decimal.Parse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture);
                        
                        if (amountValue != 0)
                        {
                            amount = amountValue;
                            break;
                        }
                    }
                }
            }
            else if (moneyMatches.Count == 1)
            {
                // Only balance found, no transaction amount
                // This could be:
                // 1. An incomplete transaction line (like "02/12/2025 Insf. Funds Distrokid Musician New")
                // 2. A continuation line from a multi-line transaction
                // Check if this might be a continuation - if so, try to merge with previous line
                if (i > 0 && results.Count > 0)
                {
                    // Check if previous transaction might be incomplete
                    var prevTransaction = results[results.Count - 1];
                    // If previous transaction description is very short or ends with incomplete word,
                    // this might be a continuation
                    if (prevTransaction.Description.Length < 30 || 
                        prevTransaction.Description.EndsWith("New", StringComparison.OrdinalIgnoreCase) ||
                        prevTransaction.Description.EndsWith("Us", StringComparison.OrdinalIgnoreCase))
                    {
                        // This might be a continuation - skip for now (will be handled by multi-line logic)
                        continue;
                    }
                }
                // Skip incomplete lines
                continue;
            }

            // Extract description - everything before the transaction amount
            // CRITICAL FIX: Use the actual transaction amount we determined, not a guess based on position
            // Find the position of the transaction amount in the line
            string? transactionAmountStr = null;
            int transactionAmountIndex = -1;
            
            // Find which monetary value matches our determined amount
            // Try to find the transaction amount in the line
            for (int j = 0; j < moneyMatches.Count - 1; j++) // Exclude balance (last one)
            {
                var matchStr = moneyMatches[j].Value.Replace(" ", "");
                var matchValue = decimal.Parse(matchStr, NumberStyles.Any, CultureInfo.InvariantCulture);
                
                // Check if this matches our determined amount (with small tolerance for rounding)
                if (Math.Abs(matchValue - amount) < 0.01m)
                {
                    transactionAmountStr = moneyMatches[j].Value; // Keep original with spaces if any
                    // Try LastIndexOf first (in case amount appears multiple times)
                    transactionAmountIndex = restOfLine.LastIndexOf(transactionAmountStr, StringComparison.Ordinal);
                    
                    // If LastIndexOf fails, try IndexOf
                    if (transactionAmountIndex < 0)
                    {
                        transactionAmountIndex = restOfLine.IndexOf(transactionAmountStr, StringComparison.Ordinal);
                    }
                    
                    // If still not found, try without spaces
                    if (transactionAmountIndex < 0)
                    {
                        var amountWithoutSpaces = amount.ToString("F2", CultureInfo.InvariantCulture);
                        if (amount < 0) amountWithoutSpaces = "-" + amountWithoutSpaces.TrimStart('-');
                        transactionAmountIndex = restOfLine.IndexOf(amountWithoutSpaces, StringComparison.Ordinal);
                    }
                    
                    if (transactionAmountIndex >= 0)
                    {
                        break; // Found it!
                    }
                }
            }
            
            // Fallback: if we couldn't find the exact amount, use the first monetary value (transaction amount)
            if (transactionAmountIndex < 0 && moneyMatches.Count > 1)
            {
                transactionAmountStr = moneyMatches[0].Value;
                transactionAmountIndex = restOfLine.IndexOf(transactionAmountStr, StringComparison.Ordinal);
                
                // If still not found, try without spaces
                if (transactionAmountIndex < 0)
                {
                    var firstAmountStr = moneyMatches[0].Value.Replace(" ", "");
                    transactionAmountIndex = restOfLine.IndexOf(firstAmountStr, StringComparison.Ordinal);
                }
            }
            
            // Extract description - handle edge cases
            string rawDescription;
            if (transactionAmountIndex > 0)
            {
                rawDescription = restOfLine.Substring(0, transactionAmountIndex).Trim();
            }
            else if (transactionAmountIndex == 0)
            {
                // Amount is at the start - description might be empty or we need to handle differently
                // This shouldn't happen in normal bank statements, but handle gracefully
                rawDescription = "Transaction";
            }
            else
            {
                // Fallback: use everything before the first monetary value
                if (moneyMatches.Count > 0)
                {
                    var firstMoneyIndex = restOfLine.IndexOf(moneyMatches[0].Value, StringComparison.Ordinal);
                    rawDescription = firstMoneyIndex > 0 
                        ? restOfLine.Substring(0, firstMoneyIndex).Trim()
                        : "Transaction";
                }
                else
                {
                    rawDescription = restOfLine.Trim();
                }
            }

            // Extract category before cleaning description
            string? category = ExtractCategory(rawDescription);
            
            // Clean up description - remove trailing category words if they exist
            var description = CleanDescription(rawDescription);

            // CRITICAL VALIDATION: Ensure we have a valid transaction amount
            // This prevents missing valid transactions like "16/12/2025 Banking App External PayShap Payment: King Digital Payments -100.00 -6.00 43.56"
            if (amount == 0)
            {
                continue; // Skip incomplete transactions
            }

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
            catch (Exception)
            {
                // Skip lines with invalid dates or parsing errors
                // In production, consider logging these for debugging
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

