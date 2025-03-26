using System.Collections.Specialized;
using System.IO;
using System.Net;

namespace MultiThreadedDownloaderLib
{
	public class HttpRequestSenderParameters
	{
		public string Method { get; set; }
		public string Url { get; set; }
		public Stream Body { get; set; }
		public NameValueCollection Headers { get; set; }
		public CookieContainer Cookies { get; set; }
		public IWebProxy Proxy { get; set; }
		public int Timeout { get; set; }
		public bool SendExpect100ContinueHeader { get; set; }
	}
}
