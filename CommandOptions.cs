using CommandLine;
using Newtonsoft.Json.Linq;
using SmartFormat;
using SmartFormat.Core.Output;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace TallSequoia.SSLLabsScan
{
	public class CommandOptions
	{
		/// <summary>
		/// match and clean a uri in to the FQDN component
		/// </summary>
		internal static readonly Regex UriCleaner = new Regex("^(?:s?(?:ht|f)tps?\\:\\/\\/)?(?:[\\d\\w]+\\:[\\d\\w]+@)?([\\d\\w\\.\\-]+)(?:\\/.*)?$", RegexOptions.Compiled);
		/*
		This aims to allow:
			Common prefixes, such as sftp, https before a ://
		  A possible <username>:<password>@ format for usernames and passwords
			A very lenient FQDN selector 
				- doesn't validate the existence of a full stop so would allow localhost or 127.0.0.1 or 1234568 as a valid hostname and teo ip address formats respectively
				- doesn't validate rules
				- permits punycode for IDNs
				- BUT doesn't permit unicode urls
		Tested as correct behaviour with:
			xn--hxajbheg2az3al.xn--jxalpdlp
			127.0.0.1
			123456789
			localhost
			https://www.cake.com/
			http://cake.com
			ftp://cake.com
			ftps://cake.com
			sftp://cake.com
			https://foo:password@example.com
		Fails with:
			παράδειγμα.δοκιμή
		*/


		// Target options

		/// <summary>
		/// Hostnames to check
		/// </summary>
		[Option('t', "hostnames", Required = false, HelpText = "Hostnames to scan (comma separated). e.g. rod.example.com,jane.example.com", Separator = ',')]
		public IList<string> Hostnames { get; set; } = new List<string>();


		[Option('i', "import", Required = false, HelpText = "File lists to process (comma separated).", Separator = ',')]
		public IList<string> ImportFilenames { get; set; } = new List<string>();


		[Option('o', "output", Required = false, HelpText = "File to save to. Use {hostname} and {date} for variables.")]
		public string OutputFilename { get; set; }


		/// <summary>
		/// Maximum number of checks with the server
		/// </summary>
		[Option("maxtries", Required = false, Default = 60, HelpText = "Maximum number of checks for updates.")]
		public int MaxTries { get; set; }


		/// <summary>
		/// Time to pause between checking for updates (s)
		/// </summary>
		[Option("pausetime", Required = false, Default = 4, HelpText = "Time to pause between checking for updates (s).")]
		public int PauseTime { get; set; }


		[Option('d', "adaptivedelay", Required = false, Default = true, HelpText = "Use feedback from SSLLabs to define delay")]
		public bool AdaptiveDelay { get; internal set; }


		/// <summary>
		/// Maximum number of scans done in parallel
		/// </summary>
		[Option("maxparallel", Required = false, Default = 1, HelpText = "Maximum number of parallel scans.")]
		public int MaxParallel { get; set; }


		/// <summary>
		/// Verbosity
		/// </summary>
		[Option('v', "verbosity", Required = false, Default = 2, HelpText = "Level of detail to show (None, Errors, Standard, Detailed or Responses)")]
		public Verbosity Verbosity { get; set; }



		// API options - https://github.com/ssllabs/ssllabs-scan/blob/master/ssllabs-api-docs-v3.md


		/// <summary>
		/// Publish the results
		/// </summary>
		[Option('p', "publish", Required = false, Default = false, HelpText = "Publish the results.")]
		public bool Publish { get; set; }


		/// <summary>
		/// Caching strategy
		/// </summary>
		/// <remarks>Maps to the fromCache and startNew API options</remarks>
		[Option('c', "cachemode", Required = false, Default = CacheMode.Optimised, HelpText = "Caching strategy. (Options: Optimised, Never, Always).")]
		public CacheMode CacheMode { get; set; }


		/// <summary>
		/// Maximum age of the cache results
		/// </summary>
		/// <remarks>Only used if the Cache Mode permits cached responses</remarks>
		[Option('a', "maxage", Required = false, Default = 23, HelpText = "Maximum age of the cache results (hours).")]
		public int MaxAge { get; set; }


		/// <summary>
		/// Detail level
		/// </summary>
		[Option('l', "level", Required = false, Default = DetailLevel.Normal, HelpText = "Level of detail. (Options: Score, Normal, Detailed)")]
		public DetailLevel DetailLevel { get; set; }


		/// <summary>
		/// Proceed with assessments even when the server certificate doesn't match the assessment hostname?
		/// </summary>
		/// <remarks>Only used if the Cache Mode permits cached responses</remarks>
		[Option('m', "ignoremismatch", Required = false, Default = false, HelpText = "Proceed with assessments even when the server certificate doesn't match the assessment hostname?")]
		public bool IgnoreMismatch { get; set; }




		/// <summary>
		/// Internal target, combining external hostnames and all imported ones
		/// </summary>
		public List<string> Targets { get; set; } = new List<string>();




		/// <summary>
		/// Load the imports
		/// </summary>
		/// <param name="options"></param>
		internal static List<string> PopulateTargets(ref CommandOptions options)
		{
			List<string> errors = new List<string>();
			string lineOfText;


			// Transfer each hostname in to the process one from the user defined list, cleaning and removing dupes as we go
			foreach (string hostname in options.Hostnames)
			{
				Match targetMatcher = UriCleaner.Match(hostname);
				string cleanTarget = targetMatcher.Groups[1].Value;

				if (cleanTarget == null || cleanTarget.Length < 4 || cleanTarget.Length > 255)
				{
					errors.Add(String.Format("Invalid target: {0} which was cleaned as {1}", hostname, cleanTarget));
				}
				else if (!options.Targets.Contains(cleanTarget))
				{
					options.Targets.Add(cleanTarget);
				}
			}


			// Load hostnames from the import file(s)
			foreach (var fileName in options.ImportFilenames)
			{
				if (!File.Exists(fileName))
				{
					errors.Add("Invalid filename: " + fileName);
					continue;
				}

				try
				{
					using (var filestream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
					using (var file = new StreamReader(filestream, System.Text.Encoding.Default, true, 4096))
					{
						while ((lineOfText = file.ReadLine()) != null)
						{
							string cleanLine = lineOfText.Trim();

							if (cleanLine.StartsWith("#")) continue;        // Comment line

							Match targetMatcher = UriCleaner.Match(cleanLine);
							string cleanTarget = targetMatcher.Groups[1].Value;

							if (cleanTarget == null || cleanTarget.Length < 4 || cleanTarget.Length > 255)
							{
								errors.Add(String.Format("Invalid target: {0} which was cleaned as {1}", cleanLine, cleanTarget));
							}
							else if (!options.Targets.Contains(cleanTarget))
							{
								options.Targets.Add(cleanTarget);
							}
						}
					}
				}
				catch (Exception)
				{
					errors.Add("Unable to read file: " + fileName);
				}
			}

			return errors;
		}


		/// <summary>
		/// Is the minimum level of the verbosity level met?
		/// </summary>
		/// <param name="requiredLevel"></param>
		/// <returns></returns>
		internal bool MinVerbosity(Verbosity requiredLevel)
		{
			return ((int)Verbosity >= (int)requiredLevel);
		}


		/// <summary>
		/// Check the options for appropriateness
		/// </summary>
		/// <param name="options"></param>
		/// <returns></returns>
		internal IList<string> CheckOptions()
		{
			List<string> errors = new List<string>();

			if (Targets.Count == 0)
			{
				errors.Add("No targets specified to scan");
			}

			if (MaxAge < 1 || MaxAge > 8760)		// 24 X 365
			{
				errors.Add("MaxAge must be between 1 and 8760 (1 year)");
			}

			if (MaxParallel < 1 || MaxParallel > 50)
			{
				errors.Add("MaxParallel must be between 1 and 50");
			}

			if (MaxTries < 1 || MaxTries > 300)
			{
				errors.Add("MaxTries must be between 1 and 300");   // LOT of tries, one per second for 5 minutes
			}

			if (PauseTime < 1 || PauseTime > 100)
			{
				errors.Add("PauseTime must be between 1 and 100");
			}


			// See if the Output filename template will be valid. e.g. catch the date without a format as this adds a colon
			string fileName = GenerateFilename(new ScanResult() { Hostname = "example.org", StartTime = DateTime.Now, Runtime = 1234, Status = QueryStatus.Success, Tries = 12, Content = JObject.Parse("{}") });
			Uri testUri = new Uri(fileName);


			// See about filename chars after the root
			bool onlyValidChars = true;
			if (testUri.Segments.Length > 2)
			{
				for(int segmentPos=2; segmentPos<testUri.Segments.Length-1; segmentPos++)
				{
					if (testUri.Segments[segmentPos].IndexOfAny(Path.GetInvalidPathChars()) != -1)
						onlyValidChars = false;
				}
				if (testUri.Segments[testUri.Segments.Length-1].IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
					onlyValidChars = false;
			}


			// See if it can generate a valid FileInfo
			bool canFileInfo = true;
			try
			{
				FileInfo testFileInfo = new FileInfo(fileName);
			}
			catch
			{
				canFileInfo = false;
			}


			if (!testUri.IsFile || !onlyValidChars || !canFileInfo)
			{
				errors.Add("OutputFilename format does not generate valid filenames. e.g. " + fileName);
			}


			return errors;
		}


		/// <summary>
		/// Create the output filename from the possible template
		/// </summary>
		/// <param name="hostname"></param>
		/// <param name="result"></param>
		/// <returns></returns>
		internal string GenerateFilename(ScanResult result)
		{
			string fileName = OutputFilename;

			// Replacement markers found
			if (fileName.Contains("{"))
			{
				// {date} is not valid as it will contain colons, so let's make it easy for users by doing a basic job of protecting from this
				fileName = fileName.Replace("{date}", "{date:yyyyMMddhhmmss}");

				var smart = Smart.CreateDefaultSmartFormat();
				smart.Settings.CaseSensitivity = SmartFormat.Core.Settings.CaseSensitivityType.CaseInsensitive;
				smart.Settings.ConvertCharacterStringLiterals = false;

				fileName = smart.Format(fileName, new Dictionary<string, object>() { { "Date", DateTime.Now }, { "Hostname", result.Hostname } });
			}

			fileName = fileName.Trim();
			return fileName;
		}
	}
}