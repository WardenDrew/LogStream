using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LogStream.API.Models
{
	public class LogListing
	{
		public string FullName { get; set; }
		public string Name { get; set; }
		public string Type { get; set; }
		public string LastModified { get; set; }
		public string FileSize { get; set; }
		public List<LogListing> Children { get; set; }
	}
}
