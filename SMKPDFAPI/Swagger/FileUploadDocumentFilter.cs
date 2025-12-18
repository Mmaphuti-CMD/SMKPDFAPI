using System.Linq;
using System.Collections.Generic;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SMKPDFAPI.Swagger;

public class FileUploadDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // This runs after all operations are generated, so we can fix file upload issues here
        foreach (var path in swaggerDoc.Paths.Values)
        {
            foreach (var operation in path.Operations.Values)
            {
                // Check if operation has IFormFile parameters
                var hasFileParam = operation.Parameters?.Any(p => 
                    p.Name == "file" && p.Schema?.Format == "binary") == true;

                if (hasFileParam && operation.RequestBody == null)
                {
                    // Convert parameter to request body
                    operation.RequestBody = new OpenApiRequestBody
                    {
                        Required = true,
                        Content = new Dictionary<string, OpenApiMediaType>
                        {
                            ["multipart/form-data"] = new OpenApiMediaType
                            {
                                Schema = new OpenApiSchema
                                {
                                    Type = "object",
                                    Properties = new Dictionary<string, OpenApiSchema>
                                    {
                                        ["file"] = new OpenApiSchema
                                        {
                                            Type = "string",
                                            Format = "binary",
                                            Description = "PDF file to upload"
                                        }
                                    },
                                    Required = new HashSet<string> { "file" }
                                }
                            }
                        }
                    };
                    
                    // Remove the file parameter
                    if (operation.Parameters != null)
                    {
                        operation.Parameters = operation.Parameters
                            .Where(p => p.Name != "file")
                            .ToList();
                    }
                }
            }
        }
    }
}
