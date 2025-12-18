namespace SMKPDFAPI.Models;

public record Transaction(DateTime Date, string Description, decimal Amount, decimal? Balance = null, string Currency = "USD");

