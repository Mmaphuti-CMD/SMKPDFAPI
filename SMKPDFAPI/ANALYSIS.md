# SMKPDFAPI Analysis Document

## Overview

SMKPDFAPI is a REST API service that extracts transaction data from Capitec Bank PDF statements. The API uses PdfPig for text extraction, regex pattern matching for transaction parsing, and includes features for duplicate detection, account information extraction, and statement metadata extraction.

## Current Features

### Core Functionality
- **PDF Text Extraction**: Uses PdfPig library to extract text from multi-page PDF statements
- **Transaction Parsing**: Regex-based parser that extracts dates, descriptions, amounts, fees, and balances
- **Duplicate Detection**: Hash-based duplicate detection with metadata tracking
- **Account Information Extraction**: Extracts account number, holder name, opening/closing balances, and totals
- **Statement Metadata**: Extracts statement date, statement number, and page count
- **Issuer Detection**: Identifies the bank issuer from statement text
- **Period Calculation**: Automatically calculates statement period and duration from transaction dates

### API Endpoints
- `GET /` - Basic API information
- `GET /rootEndpoint` - Auto-discovered endpoint listing
- `GET /api/transactions` - Endpoint information
- `POST /api/transactions` - Upload PDF and extract transactions
  - Query parameter: `debug=true` for detailed extraction information

### Response Structure
The API returns a `TransactionResponse` containing:
- `Issuer`: Bank name (e.g., "Capitec Bank")
- `PeriodStart`: First transaction date
- `PeriodEnd`: Last transaction date
- `Duration`: Human-readable period duration
- `TransactionCount`: Number of unique transactions
- `AccountInfo`: Account details (number, holder, balances, totals)
- `Metadata`: Statement metadata (date, number, pages)
- `Transactions`: List of extracted transactions

## Transaction Count Analysis

### Sample PDF Analysis

**Manual Count from PDF Text: 34 transactions** (1 incomplete)

#### Transaction List:
1. 01/11/2025 Live Better Interest Sweep Transfer -0.16 91.50
2. 01/11/2025 Banking App Transfer to Short: Transfer Transfer -53.46 38.04
3. 01/11/2025 Payment Received: M Madiope Other Income 200.00 238.04
4. 01/11/2025 Banking App External Payment: King Rental Income -195.00 -2.00 41.04
5. 15/11/2025 Payment Received: Grocery Add Up Transfer 2796356853 Other Income 100.00 141.04
6. 15/11/2025 Banking App Transfer to Short: Transfer Transfer -100.00 41.04
7. 18/11/2025 Adjustment: Refund For Incorrect Decline Fee Other Income 4.00 45.04
8. 22/11/2025 Set-off Applied Transfer -1.00 44.04
9. 23/11/2025 PayShap Payment Received: Grocery Add Up Other Income 100.00 144.04
10. 23/11/2025 Banking App Immediate Payment: Nn Dlamini Digital Payments -50.00 -1.00 93.04
11. 23/11/2025 PayShap Payment Received: Grocery Add Up Other Income 500.00 593.04
12. 23/11/2025 Banking App Immediate Payment: Master Rey Digital Payments -500.00 -1.00 92.04
13. 23/11/2025 SMS Payment Notification Fee Fees -0.35 91.69
14. 25/11/2025 PayShap Payment Received: Grocery Add Up Other Income 505.00 596.69
15. 26/11/2025 Banking App Transfer to Short: Transfer Transfer -501.00 95.69
16. 28/11/2025 Payment Received: Grocery Add Up Transfer 2827425928 Other Income 450.00 545.69
17. 29/11/2025 Banking App Immediate Payment: Screen Digital Payments -75.00 -1.00 469.69
18. 30/11/2025 Banking App Transfer to Short: Transfer Transfer -400.00 69.69
19. 30/11/2025 Interest Received Interest 0.16 69.85
20. 30/11/2025 Monthly Account Admin Fee Fees -7.50 62.35
21. 01/12/2025 Live Better Interest Sweep Transfer -0.16 62.19
22. 01/12/2025 Payment Received: Grocery Add Up Transfer 2836650527 Other Income 600.00 662.19
23. 01/12/2025 Banking App Transfer to Short: Transfer Transfer -600.13 62.06
24. 02/12/2025 Insf. Funds Distrokid Musician New (INCOMPLETE - no amount)
25. 02/12/2025 International Online Purchase Insufficient Funds Fee: Distrokid Musician New York Us Fees -2.00 60.06
26. 06/12/2025 Banking App Transfer Received from Short: Transfer Transfer 1 000.00 1 060.06
27. 06/12/2025 PayShap Payment Received: Grocery Add Up Other Income 460.00 1 520.06
28. 08/12/2025 Rana General Trading P Witbank (Card 7938) Furniture & Appliances -1 000.00 520.06
29. 09/12/2025 Banking App Prepaid Purchase: Telkom Mobile Cellphone -8.00 -0.50 511.56
30. 12/12/2025 Online Purchase: Distrokid Musician New York (Card 7938) Digital Subscriptions -459.99 51.57
31. 13/12/2025 Live Better Round-up Transfer Transfer -0.01 51.56
32. 13/12/2025 International Online Purchase Insufficient Funds Fee: Cursor, Ai Powered Ide New York Us Fees -2.00 49.56
33. 15/12/2025 Payment Received: Master Ray Other Income 100.00 149.56
34. 16/12/2025 Banking App External PayShap Payment: King Digital Payments -100.00 -6.00 43.56

### Expected Results
- **Total lines in PDF**: 34
- **Incomplete transactions**: 1 (line 24 - no amount)
- **Expected valid transactions**: 33
- **API typically returns**: 32-33 transactions (depending on duplicate detection)

### Known Issues and Challenges

1. **Incomplete Transactions**
   - Line 24: "02/12/2025 Insf. Funds Distrokid Musician New" has no monetary value
   - **Behavior**: Correctly skipped by parser (expected)

2. **Multi-line Transactions**
   - Some transactions span multiple lines in the PDF
   - Examples: "Rana General Trading" (line 28-29), "Online Purchase: Distrokid" (line 30-31)
   - **Status**: Parser handles most cases, but complex multi-line formats may require refinement

3. **Duplicate Detection**
   - Hash-based duplicate detection identifies exact duplicates
   - Transactions with identical date, description, amount, and balance are marked as duplicates
   - **Note**: Legitimate duplicate transactions (same merchant, same day) may be flagged

4. **Transaction Parsing Edge Cases**
   - Transactions with fees (e.g., "-100.00 -6.00") are correctly parsed
   - Large amounts with spaces (e.g., "1 000.00") are handled
   - Category words are removed from descriptions

## Architecture

### Key Components

1. **PdfPigTextExtractor** (`Pdf/PdfPigTextExtractor.cs`)
   - Extracts text from PDF documents
   - Returns page count from PDF structure
   - Handles multi-page documents

2. **SimpleStatementNormalizer** (`Parsing/SimpleStatementNormalizer.cs`)
   - Normalizes line breaks and whitespace
   - Splits text by date patterns when line breaks are missing
   - Filters out page numbers and empty lines

3. **RegexTransactionParser** (`Parsing/RegexTransactionParser.cs`)
   - Uses compiled regex patterns for performance
   - Identifies transaction history section
   - Extracts dates, descriptions, amounts, fees, and balances
   - Cleans transaction descriptions

4. **TransactionDuplicateDetector** (`Parsing/TransactionDuplicateDetector.cs`)
   - Generates hash IDs for transactions
   - Detects and marks duplicates
   - Provides duplicate reports

5. **BankAccountInfoExtractor** (`Parsing/BankAccountInfoExtractor.cs`)
   - Extracts account number, holder name, and type
   - Calculates opening/closing balances
   - Computes totals (credits, debits, interest)

6. **BankStatementMetadataExtractor** (`Parsing/BankStatementMetadataExtractor.cs`)
   - Extracts statement date and number
   - Uses page count from PDF structure

7. **BankIssuerExtractor** (`Parsing/BankIssuerExtractor.cs`)
   - Identifies bank issuer from statement text
   - Uses keyword matching

### Design Patterns
- **Dependency Injection**: All services registered via DI container
- **Interface Segregation**: Clean separation with interfaces
- **Single Responsibility**: Each class has a focused purpose
- **Strategy Pattern**: Parser and normalizer implementations are swappable

## Recent Code Cleanup (Latest Update)

The following cleanup was performed to improve code quality:

1. **Removed Unused Code**
   - Removed unused `ExtractTextAsync` method from `IPdfTextExtractor` interface and implementation
   - Removed commented-out code for disabled page markers in `PdfPigTextExtractor`
   - Removed dead code checking for `___PAGE_X___` markers in `SimpleStatementNormalizer`

2. **Simplified Comments**
   - Removed redundant and verbose comments
   - Kept essential documentation comments
   - Cleaned up debug-specific analysis code

3. **Code Quality**
   - All code compiles with 0 warnings and 0 errors
   - Improved maintainability and readability
   - Preserved all functional code and necessary comments

## Testing and Debugging

### Debug Mode
Enable debug mode by adding `?debug=true` to the API request:
```
POST /api/transactions?debug=true
```

Debug response includes:
- Raw extracted text length and preview
- Normalized lines count and preview
- Transaction counts (raw, unique, duplicates)
- Duplicate report
- All parsed transactions (before duplicate removal)
- Account information
- Statement metadata
- Analysis summary

### Common Issues

1. **Missing Transactions**
   - Check if transaction matches regex pattern
   - Verify transaction is in "Transaction History" section
   - Check for multi-line transaction issues

2. **Incorrect Amounts**
   - Verify PDF text extraction quality
   - Check for formatting issues (spaces in thousands)
   - Verify fee parsing logic

3. **Duplicate Detection**
   - Review duplicate report in debug mode
   - Check hash generation logic
   - Verify transaction comparison logic

## Future Enhancements

Potential improvements for consideration:

1. **Multi-line Transaction Handling**
   - Improve detection and merging of multi-line transactions
   - Better handling of wrapped descriptions

2. **Transaction Categorization**
   - Enhanced category detection
   - Custom category mapping

3. **Period Detection**
   - Extract period from statement headers
   - More accurate period calculation

4. **Additional Bank Support**
   - Support for other South African banks
   - Configurable parser patterns

5. **Performance Optimization**
   - Caching for frequently accessed statements
   - Batch processing for multiple PDFs

6. **Error Handling**
   - More detailed error messages
   - Validation for PDF format compatibility

## Notes

- The API is optimized for Capitec Bank PDF statement format
- Date parsing assumes DD/MM/YYYY format
- Currency is hardcoded to ZAR (South African Rand)
- Maximum file size: 20MB
- Transaction period is calculated from transaction dates, not statement headers
