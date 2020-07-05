using Newtonsoft.Json.Linq;
using System;

namespace TallSequoia.SSLLabsScan
{
	/// <summary>
	/// Result of the scan
	/// </summary>
	internal class ScanResult
	{
		/// <summary>
		/// Hostname
		/// </summary>
		public string Hostname { get; internal set; }

		/// <summary>
		/// JSON content returned
		/// </summary>
		internal JObject Content { get; set; }

		/// <summary>
		/// Status of the response
		/// </summary>
		internal QueryStatus Status { get; set; }

		/// <summary>
		/// Milliseconds to complete the process
		/// </summary>
		public long Runtime { get; set; }

		/// <summary>
		/// Queries of the server
		/// </summary>
		public int Tries { get; internal set; }

		/// <summary>
		/// Time the scan began
		/// </summary>
		public DateTime StartTime { get; internal set; }
	}
}
