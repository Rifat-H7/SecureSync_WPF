using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace SecureSync_Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileUploadController : ControllerBase
    {
        private readonly IHubContext<FileUploadHub> _hubContext;

        public FileUploadController(IHubContext<FileUploadHub> hubContext)
        {
            _hubContext = hubContext;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] FileChunkModel fileChunk)
        {
            var filePath = Path.Combine("UploadedFiles", fileChunk.FileName);

            try
            {
                if (!Directory.Exists("UploadedFiles"))
                    Directory.CreateDirectory("UploadedFiles");

                using (var stream = new FileStream(filePath, FileMode.Append))
                {
                    await fileChunk.FileChunk.CopyToAsync(stream);
                }

                // Notify client of success
                await _hubContext.Clients.All.SendAsync("ReceiveMessage", fileChunk.FileName, fileChunk.ChunkNumber, "Chunk uploaded successfully");

                return Ok(new { Success = true });
            }
            catch
            {
                return BadRequest(new { Success = false, Message = "Failed to upload chunk." });
            }
        }

        [HttpGet("download/{fileName}")]
        public IActionResult Download(string fileName)
        {
            var filePath = Path.Combine("UploadedFiles", fileName);
            if (!System.IO.File.Exists(filePath))
                return NotFound();

            var stream = new FileStream(filePath, FileMode.Open);
            return File(stream, "application/octet-stream", fileName);
        }
    }

    public class FileChunkModel
    {
        public string FileName { get; set; }
        public int ChunkNumber { get; set; }
        public IFormFile FileChunk { get; set; }
    }
}
