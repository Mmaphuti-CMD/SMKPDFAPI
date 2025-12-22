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
        var accountType = ExtractAccountType(headerText);
        var openingBalance = ExtractOpeningBalance(headerText, transactions);
        var closingBalance = ExtractClosingBalance(headerText, transactions);
        
        // Calculate totals from transactions
        var totals = CalculateTotals(transactions);
        
        // Try to extract interest from header
        var interestEarned = ExtractInterestEarned(headerText);
        var interestCharged = ExtractInterestCharged(headerText);

        return new AccountInfo(
            AccountNumber: accountNumber,
            AccountHolderName: accountHolderName,
            AccountType: accountType,
            OpeningBalance: openingBalance ?? (transactions.Count > 0 ? transactions.First().Balance : null),
            ClosingBalance: closingBalance ?? (transactions.Count > 0 ? transactions.Last().Balance : null),
            TotalCredits: totals.TotalCredits,
            TotalDebits: totals.TotalDebits,
            TotalFees: totals.TotalFees,
            InterestEarned: interestEarned,
            InterestCharged: interestCharged
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
        // Look for patterns like: "Account Holder: John Doe" or "Name: John Doe"
        var namePatterns = new[]
        {
            new Regex(@"(?:Account\s+Holder|Name|Account\s+Name|Holder)\s*:?\s*([A-Z][a-zA-Z\s]+(?:[A-Z][a-zA-Z\s]+)*)", RegexOptions.IgnoreCase),
            new Regex(@"Mr\.?\s+([A-Z][a-zA-Z\s]+)", RegexOptions.IgnoreCase),
            new Regex(@"Ms\.?\s+([A-Z][a-zA-Z\s]+)", RegexOptions.IgnoreCase),
            new Regex(@"Mrs\.?\s+([A-Z][a-zA-Z\s]+)", RegexOptions.IgnoreCase)
        };

        foreach (var line in headerLines)
        {
            foreach (var pattern in namePatterns)
            {
                var match = pattern.Match(line);
                if (match.Success && match.Groups.Count > 1)
                {
                    var name = match.Groups[1].Value.Trim();
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
                totalDebits += Math.Abs(transaction.Amount);
            }

            if (transaction.Fee.HasValue)
            {
                totalFees += transaction.Fee.Value;
            }
        }

        return (totalCredits, totalDebits, totalFees);
    }

    private static decimal? ExtractInterestEarned(string headerText)
    {
        var patterns = new[]
        {
            new Regex(@"Interest\s+Earned\s*:?\s*([\d\s,]+\.?\d*)", RegexOptions.IgnoreCase),
            new Regex(@"Interest\s+Credit\s*:?\s*([\d\s,]+\.?\d*)", RegexOptions.IgnoreCase),
            new Regex(@"Interest\s+Income\s*:?\s*([\d\s,]+\.?\d*)", RegexOptions.IgnoreCase)
        };

        foreach (var pattern in patterns)
        {
            var match = pattern.Match(headerText);
            if (match.Success && match.Groups.Count > 1)
            {
                var interestStr = match.Groups[1].Value.Replace(" ", "").Replace(",", "");
                if (decimal.TryParse(interestStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var interest))
                {
                    return interest;
                }
            }
        }

        return null;
    }

    private static decimal? ExtractInterestCharged(string headerText)
    {
        var patterns = new[]
        {
            new Regex(@"Interest\s+Charged\s*:?\s*([\d\s,]+\.?\d*)", RegexOptions.IgnoreCase),
            new Regex(@"Interest\s+Debit\s*:?\s*([\d\s,]+\.?\d*)", RegexOptions.IgnoreCase),
            new Regex(@"Interest\s+Expense\s*:?\s*([\d\s,]+\.?\d*)", RegexOptions.IgnoreCase)
        };

        foreach (var pattern in patterns)
        {
            var match = pattern.Match(headerText);
            if (match.Success && match.Groups.Count > 1)
            {
                var interestStr = match.Groups[1].Value.Replace(" ", "").Replace(",", "");
                if (decimal.TryParse(interestStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var interest))
                {
                    return interest;
                }
            }
        }

        return null;
    }
}

