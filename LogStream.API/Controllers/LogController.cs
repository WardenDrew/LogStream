using LogStream.API.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.WebSockets;

namespace LogStream.API.Controllers
{
	[Route("api/log")]
	[ApiController]
	public class LogController  : ControllerBase
	{
		private readonly IConfiguration _config;

		public LogController(IConfiguration config)
		{
			_config = config;
		}

		[HttpGet()]
		[ProducesResponseType(200, StatusCode = 200, Type = typeof(List<LogListing>))]
		public IActionResult GetListing()
		{
			try
			{
				return StatusCode(200, LogHandler.GetAvailable(_config));
			}
			catch (Exception ex)
			{
				if (ex.Message.ToLower().Contains("denied"))
				{
					return StatusCode(403, "The server does not have permission to access the requested file. There is likely a configuration error on the server: " + ex.Message);
				}

				return StatusCode(500, ex.Message);
			}
		}

		[HttpGet("download")]
		public IActionResult GetDownload([FromQuery] string filename)
		{
			System.IO.Stream stream = null;

			try
			{
				if (!LogHandler.IsAllowed(filename, _config))
				{
					return StatusCode(403, "Requested resource not allowed");
				}

				System.IO.FileInfo fi = new System.IO.FileInfo(filename);

				stream = System.IO.File.Open(filename, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);
				if (stream == null)
				{
					return StatusCode(404);
				}

				return File(stream, "text/plain", fi.Name);
			}
			catch (Exception ex)
			{
				if (stream != null)
				{
					stream.Dispose(); // Cant use Using pattern as ControllerBase.File() leaves the GC's scope before actually returning the data
				}

				if (ex.Message.ToLower().Contains("denied"))
				{
					return StatusCode(403, "The server does not have permission to access the requested file. There is likely a configuration error on the server: " + ex.Message);
				}

				return StatusCode(500, ex.Message);

			}
		}

		[HttpGet("live")]
		public async Task GetLive([FromQuery] string filename)
		{
			try
			{
				if (!LogHandler.IsAllowed(filename, _config))
				{
					HttpContext.Response.StatusCode = 403;
					return;
				}

				if (!HttpContext.WebSockets.IsWebSocketRequest)
				{
					HttpContext.Response.StatusCode = 400;
					return;
				}

				using (WebSocket socket = await HttpContext.WebSockets.AcceptWebSocketAsync())
				{
					await LogHandler.LiveFile(socket, filename);
				}
			}
			catch (Exception ex)
			{
				if (ex.Message.ToLower().Contains("denied"))
				{
					HttpContext.Response.StatusCode = 403;
				}

				HttpContext.Response.StatusCode = 500;
				return;
			}
		}
	}
}
