using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Services;

namespace apislice.Controllers
{
    [Route("")]
    [ApiController]
    public class IndexController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            var graphOpenApi = FilterOpenApiService.GetGraphOpenApiV1();
            string result = CreateIndex(graphOpenApi);

            return new ContentResult()
            {
                Content = result,
                ContentType = "text/plain"
            };
        }


        [Route("v1.0")]
        [HttpGet]
        public IActionResult Getv10()
        {
            var graphOpenApi = FilterOpenApiService.GetGraphOpenApiV1();
            string result = CreateIndex(graphOpenApi);

            return new ContentResult()
            {
                Content = result,
                ContentType = "text/plain"
            };
        }

        [Route("beta")]
        [HttpGet]
        public IActionResult GetBeta()
        {
            var graphOpenApi = FilterOpenApiService.GetGraphOpenApiBeta();
            string result = CreateIndex(graphOpenApi);

            return new ContentResult()
            {
                Content = result,
                ContentType = "text/plain"
            };
        }


        private static string CreateIndex(OpenApiDocument graphOpenApi)
        {
            
            var indexSearch = new OpenApiOperationIndex();
            var walker = new OpenApiWalker(indexSearch);

            walker.Walk(graphOpenApi);

            var outputsb = new StringBuilder();

            outputsb.AppendLine("# OpenAPI Operations for Microsoft Graph");
            outputsb.AppendLine();
            foreach (var item in indexSearch.Index)
            {
                outputsb.AppendLine("## " + item.Key.Name);
                foreach (var op in item.Value)
                {
                    outputsb.Append("- ");
                    outputsb.AppendLine(op.OperationId);
                }
            }
            var result = outputsb.ToString();
            return result;
        }
    }
}
