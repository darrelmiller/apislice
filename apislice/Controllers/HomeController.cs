using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace apislice.Controllers
{
    [Route("")]
    [ApiController]
    public class HomeController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            Stream openAPIStream = typeof(Startup).Assembly.GetManifestResourceStream(typeof(Startup),"openapi.yml");
            return new FileStreamResult(openAPIStream,"application/vnd.oai.openapi");
        }
    }
}
