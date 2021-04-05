using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LogStream.API.Models;
using Microsoft.Extensions.Configuration;

namespace LogStream.API
{
	public class LogHandler
	{
		static readonly private string[] _fileSuffixes = { "Bytes", "KB", "MB", "GB", "TB", "PB" };

		public static List<LogListing> GetAvailable(IConfiguration config)
		{
			List<LogListing> result = new List<LogListing>();

			List<string> allowedFiles = config.GetSection("AllowedLogFiles").GetChildren().Select(x => x.Value).ToList();
			List<string> allowedDirectories = config.GetSection("AllowedLogDirectories").GetChildren().Select(x => x.Value).ToList();

			foreach (string dir in allowedDirectories)
			{
				if (!Directory.Exists(dir)) { continue; }
				DirectoryInfo di = new DirectoryInfo(dir);
				result.Add(new LogListing()
				{
					Type = "DIRECTORY",
					FullName = di.FullName,
					Name = di.Name,
					LastModified = di.LastWriteTimeUtc.ToString(),
					Children = RecurseAddDirectoryChildren(di.FullName)
				});
			}

			foreach (string file in allowedFiles)
			{
				if (!File.Exists(file)) { continue; }
				FileInfo fi = new FileInfo(file);
				result.Add(new LogListing()
				{
					Type = "FILE",
					FullName = fi.FullName,
					Name = fi.Name,
					LastModified = fi.LastWriteTimeUtc.ToString(),
					FileSize = FormatSize(fi.Length)
				});
			}

			return result;
		}

		private static List<LogListing> RecurseAddDirectoryChildren(string directoryPath)
		{
			List<LogListing> result = new List<LogListing>();
			if (!Directory.Exists(directoryPath)) { return result; }
			DirectoryInfo di = new DirectoryInfo(directoryPath);

			foreach (DirectoryInfo ndi in di.GetDirectories())
			{
				if (!ndi.Exists) { continue; }
				result.Add(new LogListing()
				{
					Type = "DIRECTORY",
					FullName = ndi.FullName,
					Name = ndi.Name,
					LastModified = di.LastWriteTimeUtc.ToString(),
					Children = RecurseAddDirectoryChildren(ndi.FullName)
				});
			}

			foreach (FileInfo fi in di.GetFiles())
			{
				result.Add(new LogListing()
				{
					Type = "FILE",
					FullName = fi.FullName,
					Name = fi.Name,
					LastModified = fi.LastWriteTimeUtc.ToString(),
					FileSize = FormatSize(fi.Length)
				});
			}

			return result;
		}

		private static string FormatSize(Int64 bytes)
		{
			int counter = 0;
			decimal number = (decimal)bytes;
			while (Math.Round(number / 1024) >= 1 && counter < (_fileSuffixes.Length - 1))
			{
				number = number / 1024;
				counter++;
			}
			return string.Format("{0:n0} {1}", number, _fileSuffixes[counter]);
		}

		public static bool IsAllowed(string log, IConfiguration config)
		{
			List<string> allowedFiles = config.GetSection("AllowedLogFiles").GetChildren().Select(x => x.Value).ToList();
			List<string> allowedDirectories = config.GetSection("AllowedLogDirectories").GetChildren().Select(x => x.Value).ToList();

			foreach (string file in allowedFiles)
			{
				if (!File.Exists(file)) { continue; }
				FileInfo fi = new FileInfo(file);
				if (fi.FullName == log)
				{
					return true;
				}
			}

			foreach (string dir in allowedDirectories)
			{
				if (!Directory.Exists(dir)) { continue; }
				DirectoryInfo di = new DirectoryInfo(dir);
				if (RecurseIsAllowedDirectoryChildren(di.FullName, log))
				{
					return true;
				}
			}

			return false;
		}

		private static bool RecurseIsAllowedDirectoryChildren(string directoryPath, string log)
		{
			if (!Directory.Exists(directoryPath)) { return false; }
			DirectoryInfo di = new DirectoryInfo(directoryPath);

			foreach (FileInfo fi in di.GetFiles())
			{
				if (fi.FullName == log)
				{
					return true;
				}
			}

			foreach (DirectoryInfo ndi in di.GetDirectories())
			{
				if (!ndi.Exists) { continue; }
				if (RecurseIsAllowedDirectoryChildren(ndi.FullName, log))
				{
					return true;
				}
			}

			return false;
		}

		public static async Task LiveFile(WebSocket socket, string filename)
		{
			long initLength = new FileInfo(filename).Length;
			long lastLength = initLength - 4096;
			if (lastLength < 0) { lastLength = 0; }

			while (socket.State == WebSocketState.Open)
			{
				try
				{
					long fileLength = new FileInfo(filename).Length;
					if (fileLength > lastLength)
					{
						using (FileStream fs = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
						{
							fs.Seek(lastLength, SeekOrigin.Begin);
							byte[] buffer = new byte[1024];

							while (true)
							{
								int numBytesRead = fs.Read(buffer, 0, buffer.Length);
								lastLength += numBytesRead;

								if (numBytesRead == 0)
								{
									break;
								}

								await socket.SendAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), WebSocketMessageType.Text, true, CancellationToken.None);
								Array.Clear(buffer, 0, buffer.Length);
							}
						}
					}
				}
				catch (Exception ex)
				{
					byte[] buffer = Encoding.UTF8.GetBytes(ex.Message);
					await socket.SendAsync(new ArraySegment<byte>(buffer, 0, buffer.Length), WebSocketMessageType.Binary, true, CancellationToken.None);
					await socket.CloseAsync(WebSocketCloseStatus.InternalServerError, "Goodbye", CancellationToken.None);
				}

				await Task.Delay(3000);
			}

			await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Goodbye", CancellationToken.None);
		}
	}
}
