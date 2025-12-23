using System.Globalization;
using System.Text.RegularExpressions;
using SMKPDFAPI.Models;

namespace SMKPDFAPI.Parsing;

public class BankAccountInfoExtractor : IAccountInfoExtractor
{
    public AccountInfo ExtractAccountInfo(StatementText text, List<Transaction> transactions)
    {
        // Check first 100 lines for account information (usually in header)
        var headerLines = text.Lines.Take(100).ToList();
        var headerText = string.Join(" ", headerLines);

        var accountNumber = ExtractAccountNumber(headerLines);
        var accountHolderName = ExtractAccountHolderName(headerLines);
        
        // Fallback: If account holder name not found, use statement number
        // Statement number often contains the account holder identifier (e.g., "MRKUTULLO")
        if (string.IsNullOrWhiteSpace(accountHolderName))
        {
            var statementNumber = ExtractStatementNumber(headerText);
            if (!string.IsNullOrWhiteSpace(statementNumber))
            {
                accountHolderName = statementNumber;
            }
        }
        
        var accountType = ExtractAccountType(headerText);
        var openingBalance = ExtractOpeningBalance(headerText, transactions);
        var closingBalance = ExtractClosingBalance(headerText, transactions);
        
        // Calculate totals from transactions
        var totals = CalculateTotals(transactions);
        
        // Calculate totalInterestEarned from transactions with description "Interest Received"
        var totalInterestEarned = CalculateTotalInterestEarned(transactions);
        
        // totalInterestCharged = totalFees (fees are interest charges)
        var totalInterestCharged = totals.TotalFees;

        return new AccountInfo(
            AccountNumber: accountNumber,
            AccountHolderName: accountHolderName,
            AccountType: accountType,
            OpeningBalance: openingBalance ?? (transactions.Count > 0 ? transactions.First().Balance : null),
            ClosingBalance: closingBalance ?? (transactions.Count > 0 ? transactions.Last().Balance : null),
            TotalCredits: totals.TotalCredits,
            TotalDebits: totals.TotalDebits,
            TotalInterestEarned: totalInterestEarned,
            TotalInterestCharged: totalInterestCharged
        );
    }

    private static string? ExtractAccountNumber(List<string> headerLines)
    {
        // Look for patterns like: "Account Number: 1234567890" or "Account: ****1234"
        var accountPatterns = new[]
        {
            new Regex(@"Account\s+(?:Number|No\.?|#)\s*:?\s*([\d\s\*\-]+)", RegexOptions.IgnoreCase),
            new Regex(@"Account\s*:?\s*([\d\s\*\-]{4,})", RegexOptions.IgnoreCase),
            new Regex(@"Acc\s*:?\s*([\d\s\*\-]{4,})", RegexOptions.IgnoreCase)
        };

        foreach (var line in headerLines)
        {
            foreach (var pattern in accountPatterns)
            {
                var match = pattern.Match(line);
                if (match.Success && match.Groups.Count > 1)
                {
                    var accountNum = match.Groups[1].Value.Trim();
                    if (accountNum.Length >= 4)
                    {
                        return accountNum;
                    }
                }
            }
        }

        return null;
    }

    private static string? ExtractAccountHolderName(List<string> headerLines)
    {
        // Strategy 1: Find the line that is right above an address line
        // Address lines typically start with a number (street number) or contain address keywords
        // The account holder name is always right above the address on the left top side
        // Example:
        // "Main Account Statement"
        // "MR KUTULLO MMAPHUTI MADIOPE"  <- Account holder name
        // "958 MOSES KOTANE STREET..."   <- Address (starts with number)
        
        for (int i = 0; i < headerLines.Count - 1; i++)
        {
            var currentLine = headerLines[i].Trim();
            var nextLine = headerLines[i + 1].Trim();
            
            // Check if next line looks like an address (starts with number or contains address keywords)
            // More robust patterns to catch addresses
            bool isAddressLine = 
                Regex.IsMatch(nextLine, @"^\d+\s+[A-Z]") || // Starts with number followed by letter (e.g., "958 MOSES")
                Regex.IsMatch(nextLine, @"^\d+\s+\d+") || // Starts with number followed by number (e.g., "958 123")
                Regex.IsMatch(nextLine, @"\b(STREET|ST|ROAD|RD|AVENUE|AVE|DRIVE|DR|LANE|LN|BOULEVARD|BLVD|CIRCLE|CIR|COURT|CT)\b", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(nextLine, @"\b(CITY|TOWN|PARK|ZONE|AREA|EKURHULENI|STELLENBOSCH)\b", RegexOptions.IgnoreCase) ||
                (Regex.IsMatch(nextLine, @"\d{4}") && Regex.IsMatch(nextLine, @"\b(STREET|ROAD|PARK|CITY)\b", RegexOptions.IgnoreCase)); // Contains postal code and address keyword
            
            if (isAddressLine)
            {
                // Current line is likely the account holder name
                var name = currentLine.Trim();
                
                // Exclude transaction-like text (contains "Fee", "Payment", amounts, etc.)
                bool isTransactionLike = 
                    name.Contains("Fee", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Payment", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Notification", StringComparison.OrdinalIgnoreCase) ||
                    Regex.IsMatch(name, @"\d+\.\d{2}") || // Contains monetary amounts
                    name.Contains("Category", StringComparison.OrdinalIgnoreCase);
                
                if (isTransactionLike)
                {
                    continue; // Skip this line, it's not a name
                }
                
                // Validate: should contain letters, spaces, and be reasonable length
                // Allow common prefixes like MR, MRS, MS, DR, PROF
                // Pattern: Optional prefix followed by name (uppercase letters and spaces)
                if (Regex.IsMatch(name, @"^(MR|MRS|MS|DR|PROF)\.?\s+[A-Z][A-Z\s]{2,50}$", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(name, @"^[A-Z][A-Z\s]{2,50}$"))
                {
                    if (name.Length >= 3 && name.Length <= 100)
                    {
                        return name;
                    }
                }
            }
        }
        
        // Strategy 2: Look for name pattern directly (MR/MRS/MS followed by uppercase name)
        // This catches cases where address detection might fail
        for (int i = 0; i < headerLines.Count; i++)
        {
            var line = headerLines[i].Trim();
            
            // Skip transaction-like lines
            var lineLower = line.ToLowerInvariant();
            bool isTransactionLike = 
                (lineLower.Contains("fee") && (lineLower.Contains("payment") || lineLower.Contains("notification"))) ||
                lineLower.Contains("category") ||
                lineLower.Contains("money in") ||
                lineLower.Contains("money out") ||
                lineLower.Contains("balance") ||
                Regex.IsMatch(line, @"\d+\.\d{2}"); // Contains monetary amounts
            
            if (isTransactionLike)
            {
                continue;
            }
            
            // Look for name pattern: MR/MRS/MS followed by uppercase name (2-4 words, all caps)
            var nameMatch = Regex.Match(line, @"^(MR|MRS|MS|DR|PROF)\.?\s+([A-Z][A-Z\s]{5,60})$", RegexOptions.IgnoreCase);
            if (nameMatch.Success)
            {
                var fullName = nameMatch.Value.Trim();
                // Validate it looks like a name (has 2-4 words, reasonable length)
                var wordCount = fullName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
                if (wordCount >= 2 && wordCount <= 6 && fullName.Length >= 5 && fullName.Length <= 100)
                {
                    return fullName;
                }
            }
        }
        
        // Fallback: Look for patterns like "Account Holder: John Doe" or "Name: John Doe"
        // BUT exclude transaction-like text
        var namePatterns = new[]
        {
            new Regex(@"(?:Account\s+Holder|Name|Account\s+Name|Holder)\s*:?\s*([A-Z][a-zA-Z\s]+(?:[A-Z][a-zA-Z\s]+)*)", RegexOptions.IgnoreCase),
            new Regex(@"Mr\.?\s+([A-Z][a-zA-Z\s]+)", RegexOptions.IgnoreCase),
            new Regex(@"Ms\.?\s+([A-Z][a-zA-Z\s]+)", RegexOptions.IgnoreCase),
            new Regex(@"Mrs\.?\s+([A-Z][a-zA-Z\s]+)", RegexOptions.IgnoreCase)
        };

        foreach (var line in headerLines)
        {
            // Skip transaction-like lines in fallback - be very aggressive
            var lineLower = line.ToLowerInvariant();
            bool isTransactionLike = 
                (lineLower.Contains("fee") && (lineLower.Contains("payment") || lineLower.Contains("notification"))) ||
                lineLower.Contains("category") ||
                lineLower.Contains("money in") ||
                lineLower.Contains("money out") ||
                lineLower.Contains("balance") ||
                Regex.IsMatch(line, @"\d+\.\d{2}"); // Contains monetary amounts
            
            if (isTransactionLike)
            {
                continue; // Skip transaction-like lines
            }
            
            foreach (var pattern in namePatterns)
            {
                var match = pattern.Match(line);
                if (match.Success && match.Groups.Count > 1)
                {
                    var name = match.Groups[1].Value.Trim();
                    
                    // Additional validation: exclude transaction-like names
                    var nameLower = name.ToLowerInvariant();
                    if (nameLower.Contains("fee") || nameLower.Contains("payment") || nameLower.Contains("notification"))
                    {
                        continue; // Skip this match
                    }
                    
                    if (name.Length >= 3 && name.Length <= 100)
                    {
                        return name;
                    }
                }
            }
        }

        return null;
    }

    private static string? ExtractAccountType(string headerText)
    {
        var accountTypes = new[]
        {
            "Savings Account", "Savings", "Current Account", "Current",
            "Checking Account", "Checking", "Credit Card", "Credit",
            "Transaction Account", "Transaction", "Deposit Account", "Deposit"
        };

        foreach (var type in accountTypes)
        {
            if (headerText.Contains(type, StringComparison.OrdinalIgnoreCase))
            {
                return type;
            }
        }

        return null;
    }

    private static decimal? ExtractOpeningBalance(string headerText, List<Transaction> transactions)
    {
        // Look for "Opening Balance", "Beginning Balance", "Starting Balance"
        var patterns = new[]
        {
            new Regex(@"(?:Opening|Beginning|Starting)\s+Balance\s*:?\s*([\d\s,]+\.?\d*)", RegexOptions.IgnoreCase),
            new Regex(@"Balance\s+at\s+Start\s*:?\s*([\d\s,]+\.?\d*)", RegexOptions.IgnoreCase)
        };

        foreach (var pattern in patterns)
        {
            var match = pattern.Match(headerText);
            if (match.Success && match.Groups.Count > 1)
            {
                var balanceStr = match.Groups[1].Value.Replace(" ", "").Replace(",", "");
                if (decimal.TryParse(balanceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var balance))
                {
                    return balance;
                }
            }
        }

        // Fallback: use first transaction's balance (if available)
        if (transactions.Count > 0)
        {
            var firstBalance = transactions.First().Balance;
            if (firstBalance.HasValue)
            {
                return firstBalance.Value;
            }
        }

        return null;
    }

    private static decimal? ExtractClosingBalance(string headerText, List<Transaction> transactions)
    {
        // Look for "Closing Balance", "Ending Balance", "Final Balance"
        var patterns = new[]
        {
            new Regex(@"(?:Closing|Ending|Final)\s+Balance\s*:?\s*([\d\s,]+\.?\d*)", RegexOptions.IgnoreCase),
            new Regex(@"Balance\s+at\s+End\s*:?\s*([\d\s,]+\.?\d*)", RegexOptions.IgnoreCase),
            new Regex(@"New\s+Balance\s*:?\s*([\d\s,]+\.?\d*)", RegexOptions.IgnoreCase)
        };

        foreach (var pattern in patterns)
        {
            var match = pattern.Match(headerText);
            if (match.Success && match.Groups.Count > 1)
            {
                var balanceStr = match.Groups[1].Value.Replace(" ", "").Replace(",", "");
                if (decimal.TryParse(balanceStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var balance))
                {
                    return balance;
                }
            }
        }

        // Fallback: use last transaction's balance (if available)
        if (transactions.Count > 0)
        {
            var lastBalance = transactions.Last().Balance;
            if (lastBalance.HasValue)
            {
                return lastBalance.Value;
            }
        }

        return null;
    }

    private static string? ExtractStatementNumber(string headerText)
    {
        // Look for patterns like: "Statement Number: MRKUTULLO", "Statement #: MRKUTULLO", "Stmt No: MRKUTULLO"
        // The statement number often contains the account holder identifier
        var numberPatterns = new[]
        {
            new Regex(@"(?:Statement\s+(?:Number|No\.?|#)|Stmt\s+(?:No\.?|Number|#))\s*:?\s*([A-Z0-9\-]+)", RegexOptions.IgnoreCase),
            new Regex(@"Statement\s*:?\s*([A-Z0-9\-]{3,})", RegexOptions.IgnoreCase),
            new Regex(@"Ref(?:erence|\.?)\s*:?\s*([A-Z0-9\-]{3,})", RegexOptions.IgnoreCase)
        };

        foreach (var pattern in numberPatterns)
        {
            var match = pattern.Match(headerText);
            if (match.Success && match.Groups.Count > 1)
            {
                var statementNum = match.Groups[1].Value.Trim();
                // Validate it looks like a statement number (alphanumeric, 3-20 chars)
                if (statementNum.Length >= 3 && statementNum.Length <= 20 && 
                    Regex.IsMatch(statementNum, @"^[A-Z0-9\-]+$", RegexOptions.IgnoreCase))
                {
                    return statementNum;
                }
            }
        }

        return null;
    }

    private static (decimal TotalCredits, decimal TotalDebits, decimal TotalFees) CalculateTotals(List<Transaction> transactions)
    {
        decimal totalCredits = 0;
        decimal totalDebits = 0;
        decimal totalFees = 0;

        foreach (var transaction in transactions)
        {
            if (transaction.Amount > 0)
            {
                totalCredits += transaction.Amount;
            }
            else if (transaction.Amount < 0)
            {
                // Check if this is a fee-only transaction (amount IS the fee)
                // For fee-only transactions: category = "Fees" and fee equals the absolute amount
                // These should be counted as fees only, not as Money Out
                bool isFeeOnlyTransaction = 
                    transaction.Category != null && 
                    transaction.Category.Equals("Fees", StringComparison.OrdinalIgnoreCase) &&
                    transaction.Fee.HasValue &&
                    Math.Abs(transaction.Amount) == transaction.Fee.Value;
                
                if (!isFeeOnlyTransaction)
                {
                    // Regular Money Out transaction - count as debit
                    totalDebits += Math.Abs(transaction.Amount);
                }
            }

            // Count all fees (including fee-only transactions)
            if (transaction.Fee.HasValue)
            {
                totalFees += transaction.Fee.Value;
            }
        }

        return (totalCredits, totalDebits, totalFees);
    }

    private static decimal? CalculateTotalInterestEarned(List<Transaction> transactions)
    {
        // Calculate from transactions with description "Interest Received"
        // The description for interestEarned is ALWAYS "Interest Received"
        decimal totalInterest = 0;
        bool foundInterest = false;

        foreach (var transaction in transactions)
        {
            if (transaction.Description.Contains("Interest Received", StringComparison.OrdinalIgnoreCase))
            {
                // Interest earned is always a positive amount (credit)
                if (transaction.Amount > 0)
                {
                    totalInterest += transaction.Amount;
                    foundInterest = true;
                }
            }
        }

        return foundInterest ? totalInterest : null;
    }
}

