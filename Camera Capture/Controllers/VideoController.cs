using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Camera_Capture.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VideoController : ControllerBase
    {
        private readonly string _targetFilePath;
        private readonly string _compressedFilePath;

        public VideoController()
        {
            _targetFilePath = Path.Combine(Directory.GetCurrentDirectory(), "UploadedVideos");
            _compressedFilePath = Path.Combine(Directory.GetCurrentDirectory(), "CompressedVideos");

            if (!Directory.Exists(_targetFilePath))
            {
                Directory.CreateDirectory(_targetFilePath);
            }

            if (!Directory.Exists(_compressedFilePath))
            {
                Directory.CreateDirectory(_compressedFilePath);
            }
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadVideo([FromForm] IFormFile video)
        {
            if (video == null || video.Length == 0)
            {
                return BadRequest("No video file received.");
            }
            var name = randomName(10);

            var filePath = Path.Combine(_targetFilePath, name + Path.GetExtension(video.FileName));

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await video.CopyToAsync(stream);
            }

            // Nén video sau khi tải lên
            var compressedFilePath = Path.Combine(_compressedFilePath, name + "_compressed.mp4");
            bool compressionSuccess = await CompressVideoAsync(filePath, compressedFilePath);

            if (!compressionSuccess)
            {
                return StatusCode(500, "Error compressing video.");
            }
            // Thêm siêu dữ liệu sau khi nén video
            await AddMetadataAsync(compressedFilePath);

            return Ok(new { OriginalFilePath = filePath, CompressedFilePath = compressedFilePath });
        }

        private async Task<bool> CompressVideoAsync(string inputFilePath, string outputFilePath)
        {
            var ffmpegPath = @"C:\ffmpeg\bin\ffmpeg.exe"; // Đảm bảo rằng FFmpeg đã được cài đặt và có thể được gọi từ đường dẫn đầy đủ
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-i \"{inputFilePath}\" -vcodec libx264 -crf 28 \"{outputFilePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using (var process = new Process { StartInfo = startInfo })
                {
                    var stdOutput = new StringWriter();
                    var stdError = new StringWriter();

                    process.OutputDataReceived += (sender, args) => stdOutput.WriteLine(args.Data);
                    process.ErrorDataReceived += (sender, args) => stdError.WriteLine(args.Data);

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    await process.WaitForExitAsync();

                    if (process.ExitCode != 0)
                    {
                        // Log error
                        Debug.WriteLine("FFmpeg Error: " + stdError.ToString());
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Debug.WriteLine("Exception: " + ex.Message);
                return false;
            }

            return true;
        }

        private async Task AddMetadataAsync(string filePath)
        {
            var ffmpegPath = @"C:\ffmpeg\bin\ffmpeg.exe"; // Đảm bảo rằng FFmpeg đã được cài đặt và có thể được gọi từ đường dẫn đầy đủ
            var metadata = new Dictionary<string, string>
            {
                { "title", "Example Video" },
                { "author", "Your Name" },
                { "description", "This is an example video with metadata." },
                { "comment", "Encoded using FFmpeg" }
            };

            foreach (var item in metadata)
            {
                var tempFilePath = $"{filePath}_temp.mp4";
                var startInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = $"-i \"{filePath}\" -metadata {item.Key}=\"{item.Value}\" -codec copy \"{tempFilePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                try
                {
                    using (var process = new Process { StartInfo = startInfo })
                    {
                        var stdOutput = new StringWriter();
                        var stdError = new StringWriter();

                        process.OutputDataReceived += (sender, args) => stdOutput.WriteLine(args.Data);
                        process.ErrorDataReceived += (sender, args) => stdError.WriteLine(args.Data);

                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();

                        await process.WaitForExitAsync();

                        if (process.ExitCode != 0)
                        {
                            // Log error
                            Debug.WriteLine("FFmpeg Error: " + stdError.ToString());
                            return;
                        }
                    }

                    // Replace the original file with the new file containing metadata
                    System.IO.File.Delete(filePath);
                    System.IO.File.Move(tempFilePath, filePath);
                }
                catch (Exception ex)
                {
                    // Log exception
                    Debug.WriteLine("Exception: " + ex.Message);
                    return;
                }
            }
        }
        string randomName(int length)
        {
            var random = new Random();
            string name = "";
            for (int i = 0; i < length; i++)
            {
                name += random.Next(10);
            }
            return name;
        }
    }
}
