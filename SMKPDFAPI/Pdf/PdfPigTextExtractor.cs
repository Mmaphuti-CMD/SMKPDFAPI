using System.Text;
using UglyToad.PdfPig;

namespace SMKPDFAPI.Pdf;

public class PdfPigTextExtractor : IPdfTextExtractor
{
    public Task<string> ExtractTextAsync(Stream pdfStream)
    {
        var builder = new StringBuilder();

        using var document = PdfDocument.Open(pdfStream);
        foreach (var page in document.GetPages())
        {
            builder.AppendLine(page.Text);
        }

        return Task.FromResult(builder.ToString());
    }

    public Task<PdfExtractionResult> ExtractTextWithPageCountAsync(Stream pdfStream)
    {
        var builder = new StringBuilder();
        int pageCount = 0;

        using var document = PdfDocument.Open(pdfStream);
        foreach (var page in document.GetPages())
        {
            pageCount++;
            // DISABLED: PDF structure page markers (___PAGE_X___)
            // Using only "Page X of Y" text pattern detection instead
            // This avoids interference with transaction parsing
            // if (pageCount > 1)
            // {
            //     builder.AppendLine($"___PAGE_{pageCount}___");
            // }
            builder.AppendLine(page.Text);
        }

        return Task.FromResult(new PdfExtractionResult(builder.ToString(), pageCount));
    }
}

