# SMKPDFAPI

A REST API service that extracts transaction data from Capitec Bank PDF statements using advanced text extraction and pattern matching. Upload a PDF statement to receive structured JSON data with dates, descriptions, amounts, fees, and balances.

## 🚀 Features

- **PDF Text Extraction**: Uses [PdfPig](https://github.com/UglyToad/PdfPig) library for robust PDF parsing
- **Multi-page Support**: Handles bank statements spanning multiple pages
- **Regex Pattern Matching**: Intelligent transaction parsing using compiled regex patterns
- **Text Normalization**: Pre-processes extracted text to handle various PDF formatting inconsistencies
- **Structured JSON Response**: Returns clean, structured transaction data in ZAR currency
- **Swagger UI Integration**: Interactive API documentation and testing interface
- **Debug Mode**: Optional debug endpoint to inspect extraction and parsing process
- **Dependency Injection**: Clean architecture with interface-based design

## 📋 Table of Contents

- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [API Endpoints](#api-endpoints)
- [Request/Response Examples](#requestresponse-examples)
- [Architecture](#architecture)
- [Configuration](#configuration)
- [Development](#development)
- [Testing](#testing)
- [License](#license)

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- Windows, Linux, or macOS
- Capitec Bank PDF statements (tested format)

## Installation

### Clone the Repository

```bash
git clone https://github.com/Mmaphuti-CMD/SMKPDFAPI.git
cd SMKPDFAPI
```

### Restore Dependencies

```bash
dotnet restore
```

### Build the Project

```bash
dotnet build
```

### Run the Application

```bash
dotnet run --project SMKPDFAPI/SMKPDFAPI.csproj
```

The API will be available at:
- **HTTP**: `http://localhost:5000`
- **HTTPS**: `https://localhost:5001` (if configured)
- **Swagger UI**: `http://localhost:5000/swagger` (Development mode only)

## API Endpoints

### Root Endpoint

**GET** `/`

Returns basic API information and available endpoints.

**Response:**
```json
{
  "message": "SMKPDFAPI - PDF Transaction Parser API",
  "info": "Visit /rootEndpoint to see all available endpoints",
  "rootEndpoint": "/rootEndpoint",
  "swagger": "/swagger"
}
```

### Root Endpoint Discovery

**GET** `/rootEndpoint`

Automatically discovers and lists all available API endpoints with their HTTP methods.

**Response:**
```json
{
  "message": "SMKPDFAPI - PDF Transaction Parser API",
  "version": "1.0",
  "endpoints": {
    "/": ["GET"],
    "/api/transactions": ["GET", "POST"],
    "/rootEndpoint": ["GET"]
  },
  "swagger": "/swagger",
  "openApi": "/swagger/v1/swagger.json"
}
```

### Get Transactions Endpoint Info

**GET** `/api/transactions`

Returns information about how to use the transaction extraction endpoint.

**Response:**
```json
{
  "message": "Use POST to upload a PDF file",
  "endpoint": "/api/transactions",
  "method": "POST",
  "contentType": "multipart/form-data"
}
```

### Extract Transactions

**POST** `/api/transactions`

Upload a Capitec Bank PDF statement to extract transaction data.

**Content-Type:** `multipart/form-data`

**Parameters:**
- `file` (required): PDF file to upload (max 20MB)
- `debug` (optional, query parameter): Set to `true` to include extraction details in response

**Request Example (cURL):**
```bash
curl -X POST "http://localhost:5000/api/transactions?debug=false" \
  -F "file=@statement.pdf"
```

**Request Example (PowerShell):**
```powershell
$filePath = "path/to/statement.pdf"
$uri = "http://localhost:5000/api/transactions"
$form = @{
    file = Get-Item -Path $filePath
}
Invoke-RestMethod -Uri $uri -Method Post -Form $form
```

**Success Response (200 OK):**
```json
{
  "issuer": "Unknown",
  "periodStart": "0001-01-01",
  "periodEnd": "0001-01-01",
  "transactions": [
    {
      "date": "2025-11-01T00:00:00",
      "description": "Payment Received: M Madiope",
      "amount": 200.00,
      "balance": 238.04,
      "currency": "ZAR"
    },
    {
      "date": "2025-11-01T00:00:00",
      "description": "Banking App External Payment: King",
      "amount": -195.00,
      "balance": 41.04,
      "currency": "ZAR"
    }
  ]
}
```

**Debug Response (200 OK with `debug=true`):**
```json
{
  "rawTextLength": 15234,
  "rawTextPreview": "Capitec Bank Statement...",
  "normalizedLinesCount": 145,
  "normalizedLinesPreview": [
    "Capitec Bank Statement",
    "Account Number: 1234567890",
    "Transaction History",
    "Date Description Money In Money Out Balance",
    "01/11/2025 Payment Received: M Madiope Other Income 200.00 238.04"
  ],
  "transactionsFound": 12,
  "transactions": [...],
  "issuer": "Unknown",
  "periodStart": "0001-01-01",
  "periodEnd": "0001-01-01"
}
```

**Error Responses:**

- **400 Bad Request**: Missing file or invalid file type
  ```json
  {
    "error": "Missing file."
  }
  ```
  or
  ```json
  {
    "error": "Only PDF allowed."
  }
  ```

## Request/Response Examples

### Example Transaction Object

```json
{
  "date": "2025-12-08T00:00:00",
  "description": "Rana General Trading P Witbank (Card 7938)",
  "amount": -1000.00,
  "balance": 520.06,
  "currency": "ZAR"
}
```

### Field Descriptions

- **date**: Transaction date in ISO 8601 format
- **description**: Cleaned transaction description (category words removed)
- **amount**: Transaction amount (positive for credits, negative for debits)
- **balance**: Account balance after the transaction
- **currency**: Always "ZAR" (South African Rand) for Capitec Bank

## Architecture

### Project Structure

```
SMKPDFAPI/
├── Controllers/
│   └── TransactionsController.cs    # API endpoint handlers
├── Models/
│   ├── Transaction.cs                # Transaction data model
│   └── TransactionResponse.cs       # API response wrapper
├── Pdf/
│   ├── IPdfTextExtractor.cs         # PDF extraction interface
│   └── PdfPigTextExtractor.cs       # PdfPig implementation
├── Parsing/
│   ├── IStatementNormalizer.cs      # Text normalization interface
│   ├── ITransactionParser.cs        # Transaction parsing interface
│   ├── RegexTransactionParser.cs    # Regex-based parser implementation
│   ├── SimpleStatementNormalizer.cs # Text normalization implementation
│   └── StatementText.cs             # Normalized text model
├── Swagger/
│   ├── FileUploadDocumentFilter.cs  # Swagger file upload configuration
│   └── FileUploadOperationFilter.cs # Swagger operation configuration
├── Program.cs                        # Application entry point
└── SMKPDFAPI.csproj                 # Project file
```

### Design Patterns

- **Dependency Injection**: All services registered via DI container
- **Interface Segregation**: Clean separation of concerns with interfaces
- **Single Responsibility**: Each class has a focused purpose
- **Strategy Pattern**: Parser and normalizer implementations are swappable

### Processing Pipeline

1. **PDF Upload**: File received via multipart/form-data
2. **Text Extraction**: PdfPig extracts raw text from all pages
3. **Normalization**: Text is cleaned, split into lines, and formatted
4. **Parsing**: Regex patterns identify and extract transaction data
5. **Response**: Structured JSON returned to client

### Key Components

#### PdfPigTextExtractor
- Extracts text from PDF documents using PdfPig library
- Handles multi-page documents
- Returns concatenated text from all pages

#### SimpleStatementNormalizer
- Normalizes line breaks and whitespace
- Handles PDFs with missing line breaks by splitting on date patterns
- Filters out page numbers and empty lines
- Identifies transaction history sections

#### RegexTransactionParser
- Uses compiled regex patterns for performance
- Identifies transaction history section
- Extracts dates, descriptions, amounts, and balances
- Cleans transaction descriptions by removing category words
- Handles various monetary formats (with/without spaces in thousands)

## Configuration

### File Size Limits

The API accepts PDF files up to **20MB** by default. This is configured in `Program.cs`:

```csharp
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 20_000_000); // 20 MB
```

### Environment Settings

Configuration files:
- `appsettings.json`: Production settings
- `appsettings.Development.json`: Development settings

### Swagger Configuration

Swagger UI is enabled in Development mode only. To enable in production, modify `Program.cs`:

```csharp
// Remove the if (app.Environment.IsDevelopment()) check
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SMKPDFAPI v1");
});
```

## Development

### Prerequisites for Development

- Visual Studio 2022 or VS Code with C# extension
- .NET 9.0 SDK
- Git

### Running in Development Mode

```bash
dotnet run --project SMKPDFAPI/SMKPDFAPI.csproj
```

The application will:
- Enable Swagger UI at `/swagger`
- Use development configuration
- Show detailed error pages

### Building for Production

```bash
dotnet publish -c Release -o ./publish
```

### Code Structure

- **Controllers**: Handle HTTP requests and responses
- **Models**: Data transfer objects (DTOs)
- **Services**: Business logic (extraction, normalization, parsing)
- **Interfaces**: Contracts for dependency injection

### Extending the Parser

To support additional bank statement formats:

1. Implement `ITransactionParser` interface
2. Register your implementation in `Program.cs`:
   ```csharp
   builder.Services.AddScoped<ITransactionParser, YourCustomParser>();
   ```

### Extending the Normalizer

To add custom text normalization:

1. Implement `IStatementNormalizer` interface
2. Register your implementation in `Program.cs`:
   ```csharp
   builder.Services.AddScoped<IStatementNormalizer, YourCustomNormalizer>();
   ```

## Testing

### Using Swagger UI

1. Navigate to `http://localhost:5000/swagger`
2. Expand the `POST /api/transactions` endpoint
3. Click "Try it out"
4. Upload a PDF file
5. Click "Execute"

### Using cURL

```bash
curl -X POST "http://localhost:5000/api/transactions" \
  -F "file=@path/to/statement.pdf"
```

### Using PowerShell

```powershell
$uri = "http://localhost:5000/api/transactions"
$filePath = "path/to/statement.pdf"
$form = @{
    file = Get-Item -Path $filePath
}
$response = Invoke-RestMethod -Uri $uri -Method Post -Form $form
$response | ConvertTo-Json -Depth 10
```

### Debug Mode

Enable debug mode to see extraction details:

```bash
curl -X POST "http://localhost:5000/api/transactions?debug=true" \
  -F "file=@statement.pdf"
```

This returns:
- Raw extracted text length and preview
- Normalized lines count and preview
- Number of transactions found
- Full transaction list

## Dependencies

- **Microsoft.AspNetCore.OpenApi** (v9.0.10): OpenAPI/Swagger support
- **Swashbuckle.AspNetCore** (v6.9.0): Swagger UI generation
- **UglyToad.PdfPig** (v1.7.0-custom-5): PDF text extraction

## Known Limitations

- Currently optimized for Capitec Bank PDF statement format
- Date parsing assumes DD/MM/YYYY format
- Currency is hardcoded to ZAR (South African Rand)
- Maximum file size: 20MB
- Transaction period detection not yet implemented (returns default values)

## Future Enhancements

- [ ] Support for multiple bank statement formats
- [ ] Automatic period detection from statement headers
- [ ] Issuer detection from statement metadata
- [ ] Support for multiple currencies
- [ ] Transaction categorization
- [ ] Export to CSV/Excel formats
- [ ] Batch processing for multiple PDFs
- [ ] Authentication and authorization
- [ ] Rate limiting
- [ ] Caching for frequently accessed statements

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Author

**Mmaphuti-CMD**

- GitHub: [@Mmaphuti-CMD](https://github.com/Mmaphuti-CMD)

## Acknowledgments

- [PdfPig](https://github.com/UglyToad/PdfPig) - PDF parsing library
- [Swashbuckle](https://github.com/domaindrivendev/Swashbuckle.AspNetCore) - Swagger/OpenAPI tooling
- Capitec Bank - For the statement format reference

## Support

For issues, questions, or contributions, please open an issue on the [GitHub repository](https://github.com/Mmaphuti-CMD/SMKPDFAPI/issues).

---

**Note**: This API is designed specifically for Capitec Bank PDF statements. For other banks, you may need to customize the parser logic.
