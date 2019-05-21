using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Services;
using System.Management.Automation;
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
            Console.WriteLine("nakul!!");
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
