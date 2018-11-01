using System.IO;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace apislice.Controllers
{
    public class StreamResult : IActionResult
    {
        private readonly Stream stream;
        private readonly MediaTypeHeaderValue contentType;

        public StreamResult(Stream stream, MediaTypeHeaderValue contentType )
        {
            this.stream = stream;
            this.contentType = contentType;
        }
        public async Task ExecuteResultAsync(ActionContext context)
        {
            var response = context.HttpContext.Response;
            response.ContentType = contentType.ToString();

            if (this.stream.CanSeek)
            {
                response.ContentLength = this.stream.Length;
            } else
            {
                response.Headers.Add("Transfer-encoding", "chunked");
            }
            await this.stream.CopyToAsync(response.Body);
            await response.Body.FlushAsync();

        }
    }
}
