using Newtonsoft.Json.Linq;
using System;
using System.Net;
using System.Text;

namespace TallSequoia.SSLLabsScan
{
	/// <summary>
	/// Scanner
	/// </summary>
	internal class Scanner
	{
		/// <summary>
		/// Base URL for the SSL Labs API
		/// </summary>
		private const string ApiRoot = "https://api.ssllabs.com/api/v3/analyze";


		/// <summary>
		/// Number of retries for the server being overloaded
		/// </summary>
		private const int MaxOverloadRetries = 9;



		/// <summary>
		/// Obtain the results of a host
		/// </summary>
		/// <param name="options"></param>
		/// <param name="hostname"></param>
		/// <returns></returns>
		internal static ScanResult ScanHost(ref CommandOptions options, string hostname)
		{
			ScanResult result = new ScanResult() { Hostname = hostname, StartTime = DateTime.UtcNow };

			string apiResult = null;

			if (options.Verbosity > 0) Console.WriteLine("[.] Scanning: {0}", hostname);

			// server can report overload, so have a limited number of tests
			int overLoadRetry = 0;

			while (overLoadRetry < MaxOverloadRetries)
			{
				using (WebClient client = new WebClient())
				{
					result.Tries = 1;
					Uri endpoint = BuildQueryUri(options, hostname, true);

					if (options.MinVerbosity(Verbosity.Standard))
						Console.WriteLine("[.] Requesting: " + endpoint.ToString());

					// Iterate for up to the permitted number of tries
					while (result.Tries <= options.MaxTries)
					{
						// Get the revised one - it can change after the first request due to the caching mode
						if (result.Tries == 2)
						{
							endpoint = endpoint = BuildQueryUri(options, hostname, false);

							if (options.MinVerbosity(Verbosity.Detailed)) Console.WriteLine("[.] Request string is now: " + endpoint.ToString());
						}


						// Obtain the response
						try
						{
							apiResult = client.DownloadString(endpoint);
						}
						catch (WebException ex)
						{
							if (ex.Status == WebExceptionStatus.Timeout)
							{
								result.Status = QueryStatus.Timeout;
							}
							else if (ex.Response != null)
							{
								switch (((HttpWebResponse)ex.Response).StatusCode)
								{
									case (HttpStatusCode)429:     // TooManyRequests - Not a const in .NET Framework target
										result.Status = QueryStatus.RateLimited;

										// Adapt to manage this if it's the first time seeing it
										if (options.MaxParallel > 1 && overLoadRetry == 0) options.MaxParallel--;

										// Sleep for a random time as there may well be other threads also hitting this
										Random sleepRandomiser = new Random();
#pragma warning disable SCS0005 // Weak random generator - this is not intended to be a strong form of random
										int rateLimitedSleepDelay = sleepRandomiser.Next(1000, 10000);
#pragma warning restore SCS0005 // Weak random generator

										if (options.MinVerbosity(Verbosity.Detailed))
											Console.WriteLine("[.] Server overloaded. Pausing {0} for {1}", hostname, rateLimitedSleepDelay);

										System.Threading.Thread.Sleep(rateLimitedSleepDelay);

										continue;

									case HttpStatusCode.ServiceUnavailable:   // 503
										result.Status = QueryStatus.Maintenance;
										break;

									case (HttpStatusCode)529:                 // 529
										result.Status = QueryStatus.Overloaded;
										break;

									case (HttpStatusCode)441:                 // Unknown. Received with host of 127.0.0.1
										result.Status = QueryStatus.WebError;
										break;

									default:
										result.Status = QueryStatus.WebError;

										if (options.Verbosity == Verbosity.Responses)
										{
											Console.WriteLine("[!] Error with the Web Server... Techie details follow:\n" + ex.ToString());
										}
										else if (options.MinVerbosity(Verbosity.Errors))
											Console.WriteLine("[!] Error with the Web Server");

										break;
								}
							}

							if (result.Status != QueryStatus.RateLimited)
								return result;
						}


						if (options.Verbosity == Verbosity.Responses)
							Console.WriteLine("[.] Raw server response: " + apiResult);


						// Convert to a parsed JSON object
						try
						{
							result.Content = JObject.Parse(apiResult);
						}
						catch (Exception)
						{
							result.Status = QueryStatus.ResponseError;
							return result;
						}


						// Check it
						string responseStatus = (string)result.Content["status"];

						if (responseStatus == "READY")
						{
							// This means it's finished, not that the test was correct. e.g. checks for a host that responds to ping but has no HTTPS endpoint (test case: amazon.ie)
							bool validScan = false;

							foreach (var item in result.Content["endpoints"])
								if ((string)item["statusMessage"] == "Ready") validScan = true;

							if (validScan)
								result.Status = QueryStatus.Success;
							else
								result.Status = QueryStatus.HostError;    // e.g. "Unable to connect to the server"

							return result;
						}

						if (responseStatus == "ERROR")
						{
							if ((string)result.Content["statusMessage"] == "Unable to resolve domain name")
								result.Status = QueryStatus.InvalidHostname;
							else
								result.Status = QueryStatus.ServiceError;

							return result;
						}


						result.Tries++;

						int pauseMs = CalculatePauseTime(options, result);

						if (options.MinVerbosity(Verbosity.Detailed))
							Console.WriteLine("[.] Pausing {1} for {0} ms", pauseMs, hostname);

						// Pause for a little mo
						System.Threading.Thread.Sleep(pauseMs);
					}
				}

				overLoadRetry++;
			}

			if (result.Status != QueryStatus.RateLimited)
				result.Status = QueryStatus.Timeout;

			return result;
		}

		/// <summary>
		/// Determina an appropriate pause time
		/// </summary>
		/// <param name="options"></param>
		/// <param name="result"></param>
		/// <returns>Time to pause (ms)</returns>
		private static int CalculatePauseTime(CommandOptions options, ScanResult result)
		{
			if (!options.AdaptiveDelay || result.Content["endpoints"] == null)
				return options.PauseTime * 1000;

			int minETA = 99;

			foreach (var item in result.Content["endpoints"])
				if (item["eta"] != null && (int)item["eta"] < minETA) minETA = (int)item["eta"];

			// ETA is how long to wait
			if (minETA < 3) return options.PauseTime * 1000;

			// Rate seems to be about 1% per second, but let's be optimistic and say every half second
			return minETA * 700;
		}


		/// <summary>
		/// Generate a query URI for 
		/// </summary>
		/// <param name="options">Options to use for the check</param>
		/// <param name="hostname">Hostname to check</param>
		/// <param name="isFirstRequest">Is this a first request for this session</param>
		/// <returns></returns>
		internal static Uri BuildQueryUri(CommandOptions options, string hostname, bool isFirstRequest)
		{
			StringBuilder builder = new StringBuilder(ApiRoot);

			// host to scan
			builder.Append("?host=");
			builder.Append(hostname);


			if (options.CacheMode == CacheMode.Never && isFirstRequest)
			{
				// startNew - if set to "on" then cached assessment results are ignored and a new assessment is started.However, if there's already an assessment in progress, its status is delivered instead. This parameter should be used only once to initiate a new assessment; further invocations should omit it to avoid causing an assessment loop.
				builder.Append("&startNew=on");
			}

			if (options.CacheMode == CacheMode.Always)
			{
				//fromCache - always deliver cached assessment reports if available; optional, defaults to "off".This parameter is intended for API consumers that don't want to wait for assessment results. Can't be used at the same time as the startNew parameter.
				builder.Append("&fromCache=on");

				//maxAge - maximum report age, in hours, if retrieving from cache (fromCache parameter set). Let the server decide if we have not set it
				if (options.MaxAge > 0)
				{
					builder.Append("&maxAge=");
					builder.Append(options.MaxAge);
				}
			}

			// publish - set to "on" if assessment results should be published on the public results boards; optional, defaults to "off".
			builder.Append("&publish=");
			builder.Append(BoolToText(options.Publish));

			// all - by default this call results only summaries of individual endpoints.If this parameter is set to "on", full information will be returned.If set to "done", full information will be returned only if the assessment is complete(status is READY or ERROR).
			if (options.DetailLevel == DetailLevel.Detailed)
				builder.Append("&all=done");


			// ignoreMismatch - set to "on" to proceed with assessments even when the server certificate doesn't match the assessment hostname. Set to off by default. Please note that this parameter is ignored if a cached report is returned.
			builder.Append("&ignoreMismatch=");
			builder.Append(BoolToText(options.IgnoreMismatch));


			return new Uri(builder.ToString());
		}


		/// <summary>
		/// Boolean value to the API's textual representation
		/// </summary>
		/// <param name="state"></param>
		/// <returns></returns>
		internal static string BoolToText(bool state)
		{
			if (state) return "on";
			return "off";
		}

	}
}
