using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Services;
using Microsoft.OpenApi.Writers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace apislice.Controllers
{
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
                                    [FromQuery]string openApiVersion = "3",
                                    [FromQuery]string title = "Partial Graph API")
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
                var operationIdsArray = operationIds.Split(',');
                predicate = (o) => operationIdsArray.Contains(o.OperationId);
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

            var subsetOpenApiDocument = FilterOpenApiService.CreateFilteredDocument(title,version, graphOpenApi, predicate);

            FilterOpenApiService.CopyReferences(graphOpenApi, subsetOpenApiDocument);

            var anyOfRemover = new AnyOfRemover();
            var walker = new OpenApiWalker(anyOfRemover);
            walker.Walk(subsetOpenApiDocument);

            return CreateResult(openApiVersion, subsetOpenApiDocument);
        }

        private static IActionResult CreateResult(string openApiVersion, OpenApiDocument subset)
        {
            var sr = new StringWriter();
            var writer = new OpenApiYamlWriter(sr);
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
