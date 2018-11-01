using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc;
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

            return new ContentResult() {
                Content = result,
                ContentType = "text/plain"
            };
        }
    }
}
