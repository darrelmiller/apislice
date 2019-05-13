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
    [Route("list")]
    [ApiController]
    public class IndexController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            var graphOpenApi = FilterOpenApiService.GetGraphOpenApiV1();
            WriteIndex(graphOpenApi, Response.Body);

            return new EmptyResult();
        }


        [Route("v1.0")]
        [HttpGet]
        public IActionResult Getv10()
        {
            var graphOpenApi = FilterOpenApiService.GetGraphOpenApiV1();

            Response.Headers["Content-Type"] = "text/html";
            WriteIndex(graphOpenApi, Response.Body);

            return new EmptyResult();
        }

        [Route("beta")]
        [HttpGet]
        public IActionResult GetBeta()
        {
            var graphOpenApi = FilterOpenApiService.GetGraphOpenApiBeta();
            WriteIndex(graphOpenApi, Response.Body);

            return new EmptyResult();
        }


        private static void WriteIndex(OpenApiDocument graphOpenApi, Stream stream)
        {
            var sw = new StreamWriter(stream);
            
            var indexSearch = new OpenApiOperationIndex();
            var walker = new OpenApiWalker(indexSearch);

            walker.Walk(graphOpenApi);

            sw.AutoFlush = true;

            sw.WriteLine("<h1># OpenAPI Operations for Microsoft Graph</h1>");
            sw.WriteLine("<b/>");
            sw.WriteLine("<ul>");
            foreach (var item in indexSearch.Index)
            {
                sw.WriteLine("<li><a href='./$openapi?tags=" + item.Key.Name+"'>" + item.Key.Name+"</a></li>");
                sw.WriteLine("<ul>");
                foreach (var op in item.Value)
                {
                    sw.WriteLine("<li><a href='./$openapi?operationIds=" + op.OperationId + "'>" + op.OperationId + "</a></li>");
                }
                sw.WriteLine("</ul>");
            }
            sw.WriteLine("</ul>");

        }
    }
}
