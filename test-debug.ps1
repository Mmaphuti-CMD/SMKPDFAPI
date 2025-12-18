# PowerShell script to test the API with debug mode
# Usage: .\test-debug.ps1 "C:\path\to\your\statement.pdf"

param(
    [Parameter(Mandatory=$true)]
    [string]$FilePath
)

$uri = "http://localhost:5180/api/transactions?debug=true"
$file = Get-Item $FilePath

Write-Host "Uploading $($file.Name) with debug mode..." -ForegroundColor Yellow

try {
    # Use .NET HttpClient for file upload
    Add-Type -AssemblyName System.Net.Http
    
    $httpClient = New-Object System.Net.Http.HttpClient
    $content = New-Object System.Net.Http.MultipartFormDataContent
    $fileStream = [System.IO.File]::OpenRead($file.FullName)
    $streamContent = New-Object System.Net.Http.StreamContent($fileStream)
    $streamContent.Headers.ContentType = New-Object System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf")
    $content.Add($streamContent, "file", $file.Name)
    
    $response = $httpClient.PostAsync($uri, $content).Result
    $responseContent = $response.Content.ReadAsStringAsync().Result
    
    $httpClient.Dispose()
    $fileStream.Close()
    
    Write-Host "`nResponse received!" -ForegroundColor Green
    Write-Host "`n" + ("=" * 80) -ForegroundColor Cyan
    Write-Host $responseContent -ForegroundColor White
    Write-Host ("=" * 80) -ForegroundColor Cyan
    
    # Pretty print JSON
    $json = $responseContent | ConvertFrom-Json
    Write-Host "`nParsed JSON:" -ForegroundColor Yellow
    $json | ConvertTo-Json -Depth 10 | Write-Host -ForegroundColor White
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    if ($_.Exception.InnerException) {
        Write-Host "Inner: $($_.Exception.InnerException.Message)" -ForegroundColor Red
    }
}
