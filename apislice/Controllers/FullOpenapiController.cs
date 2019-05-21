using System;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Services;
using Microsoft.OpenApi.Writers;

namespace apislice.Controllers
{
    [Route("full")]
    [ApiController]
    public class FullOpenapiController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            var graphOpenApi = FilterOpenApiService.GetGraphOpenApiV1();
                                   
            var anyOfRemover = new AnyOfRemover();
            var walker = new OpenApiWalker(anyOfRemover);
            walker.Walk(graphOpenApi);
                        
            var sr = new StringWriter();
            var writer = new OpenApiYamlWriter(sr);
                                             
            graphOpenApi.SerializeAsV3(writer);
            var output = sr.GetStringBuilder().ToString();

            return new ContentResult()
            {
                Content = output,
                ContentType = "application/json"
            }; 
        }
        
    }
}
