using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Microsoft.OpenApi.Readers.Interface;
using Microsoft.OpenApi.Services;
using Microsoft.OpenApi.Writers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace apislice.Controllers
{
    public enum OpenApiStyle
    {
        Powershell,
        PowerPlatform,
        Plain
    }

    /// <summary>
    /// Controller that enables querying over an OpenAPI document
    /// </summary>
    public class OpenApiController : ControllerBase
    {
        [Route("$openapi")]
        [Route("{version}/$openapi")]
        [HttpGet]
        public IActionResult Get(string version = "v1.0",
                                    [FromQuery]string operationIds = null,
                                    [FromQuery]string tags = null,
                                    [FromQuery]string openApiVersion = "2",
                                    [FromQuery]string title = "Partial Graph API",
                                    [FromQuery]OpenApiStyle style = OpenApiStyle.Plain,
                                    [FromQuery]string format = "yaml")
        {
            OpenApiDocument graphOpenApi = null;
            switch (version)
            {
                case "v1.0":
                    graphOpenApi = FilterOpenApiService.GetGraphOpenApiV1();
                    break;
                case "beta":
                    graphOpenApi = FilterOpenApiService.GetGraphOpenApiBeta();
                    break;

                default:
                    return new NotFoundResult();
            }

            if (operationIds != null && tags != null)
            {
                return new BadRequestResult();
            }

            Func<OpenApiOperation, bool> predicate = null;
            if (operationIds != null)
            {
                if (operationIds == "*") {
                    predicate = (o) => true;  // All operations
                } else {
                    var operationIdsArray = operationIds.Split(',');
                    predicate = (o) => operationIdsArray.Contains(o.OperationId);
                }
            }
            else if (tags != null)
            {
                var tagsArray = tags.Split(',');
                predicate = (o) => o.Tags.Any(t => tagsArray.Contains(t.Name));
            }
            else
            {
                return new NotFoundResult();
            }

            var subsetOpenApiDocument = FilterOpenApiService.CreateFilteredDocument(title, version, graphOpenApi, predicate);

            FilterOpenApiService.CopyReferences(graphOpenApi, subsetOpenApiDocument);

            if (style == OpenApiStyle.PowerPlatform || style == OpenApiStyle.Powershell)
            {
                // Clone doc before making changes
                subsetOpenApiDocument = Clone(subsetOpenApiDocument);

                var anyOfRemover = new AnyOfRemover();
                var walker = new OpenApiWalker(anyOfRemover);
                walker.Walk(subsetOpenApiDocument);
            }

            return CreateResult(openApiVersion, subsetOpenApiDocument, format);
        }

        private static OpenApiDocument Clone(OpenApiDocument subsetOpenApiDocument)
        {
            var stream = new MemoryStream();
            var writer = new OpenApiYamlWriter(new StreamWriter(stream));
            subsetOpenApiDocument.SerializeAsV3(writer);
            writer.Flush();
            stream.Position = 0;
            var reader = new OpenApiStreamReader();
            return reader.Read(stream, out OpenApiDiagnostic diag);
        }

        private static IActionResult CreateResult(string openApiVersion, OpenApiDocument subset, string format)
        {
            var sr = new StringWriter();
            OpenApiWriterBase writer;
            if (format == "yaml")
            {
                writer = new OpenApiYamlWriter(sr);
            }
            else
            {
                writer = new OpenApiJsonWriter(sr);
            }

            if (openApiVersion == "2")
            {
                subset.SerializeAsV2(writer);
            }
            else
            {
                subset.SerializeAsV3(writer);
            }
            var output = sr.GetStringBuilder().ToString();

            return new ContentResult()
            {
                Content = output,
                ContentType = "application/json"
            };
        }
    }
}
