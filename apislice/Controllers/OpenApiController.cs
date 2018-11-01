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
    public class OpenApiController : ControllerBase
    {
        [Route("$openapi")]
        [HttpGet]
        public IActionResult Get(string operationIds)
        {

            var graphOpenApi = FilterOpenApiService.GetGraphOpenApiV1();

            var operationIdsArray = operationIds.Split(',');

            var subset = FilterOpenApiService.CreateFilteredDocument(graphOpenApi, (o) => operationIdsArray.Contains(o.OperationId));

//            FilterOpenApiService.CopyReferences(graphOpenApi, subset);

            var sr = new StringWriter();
            var writer = new OpenApiJsonWriter(sr);
            subset.SerializeAsV2(writer);
            var output = sr.GetStringBuilder().ToString();

            return new ContentResult()
            {
                Content = output
            };
        }

    }
}
