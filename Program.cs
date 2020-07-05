using CommandLine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

//[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("UnitTests, PublicKeyToken=f7013a81c3d2cd47")]

namespace TallSequoia.SSLLabsScan
{
	class Program
	{
		/// <summary>
		/// Possible grades in order
		/// </summary>
		private static readonly string[] Grades = new string[] { "T", "F", "E", "D", "C", "B", "A", "A+" };


		/// <summary>
		/// Program entry point
		/// </summary>
		/// <param name="args"></param>
		internal static void Main(string[] args)
		{
			Parser.Default.ParseArguments<CommandOptions>(args)
			 .WithParsed(RunOptions);
		}


		/// <summary>
		/// Normal run condition
		/// </summary>
		/// <param name="options"></param>
		internal static void RunOptions(CommandOptions options)
		{
			List<string> errors = new List<string>();

			// Process the targets from the hostnames and import any files
			errors.AddRange(CommandOptions.PopulateTargets(ref options));

			// Validate entries
			errors.AddRange(options.CheckOptions());

			// Report on any errors
			if (errors.Count > 0)
			{
				Console.WriteLine("Errors found:");
				foreach (string error in errors)
					Console.WriteLine(" * " + error);
				Console.WriteLine("\nUse --help for details of command options.");
				return;
			}


			// Parallel operation, but will be serial if MaxParallel is set as 1
			Parallel.ForEach(options.Targets,
			 new ParallelOptions { MaxDegreeOfParallelism = options.MaxParallel },
			 (hostname) =>
			 {
				Stopwatch runTimer = new Stopwatch();
				runTimer.Start();


				// check we have something to scan
				ScanResult result = Scanner.ScanHost(ref options, hostname);


				runTimer.Stop();
				result.Runtime = runTimer.ElapsedMilliseconds / 1000;
				if (options.MinVerbosity(Verbosity.Standard)) Console.WriteLine("[.] Timer: {0} took {1}s", hostname, result.Runtime);


				if (result != null) ProcessResult(options, result);
			});
		}


		/// <summary>
		/// Process the output of the scan
		/// </summary>
		/// <param name="options"></param>
		/// <param name="hostname"></param>
		/// <param name="result">Compound result</param>
		private static void ProcessResult(CommandOptions options, ScanResult result)
		{
			{
				if (result.Status != QueryStatus.Success || result.Content == null)
				{
					if (options.MinVerbosity(Verbosity.Errors)) Console.WriteLine("[!] Error: {0} whilst processing {1}", result.Status, result.Hostname);
					return;
				}


				// Save
				if (!String.IsNullOrEmpty(options.OutputFilename)) Save(options, result);


				// Nothing to output if verbosity is below Standard
				if (!options.MinVerbosity(Verbosity.Standard)) return;


				string score = GetScore(result.Content);

				if (options.Verbosity != Verbosity.None)
				{
					Console.WriteLine("{0} ({1})", result.Hostname, score);

					if (options.DetailLevel != DetailLevel.Score)
						Console.WriteLine(result.Content);
				}
			}
		}

		/// <summary>
		/// Save it
		/// </summary>
		/// <param name="options"></param>
		/// <param name="hostname"></param>
		/// <param name="result"></param>
		private static void Save(CommandOptions options, ScanResult result)
		{
			// Format the filename
			string fileName = options.GenerateFilename(result);

			try
			{
				// If this is a directory, then we will add default naming  [don't use EndsWith as this doesn't support char in targetted .NET Framework]
				if (Directory.Exists(fileName) || fileName[fileName.Length-1] == Path.DirectorySeparatorChar || fileName[fileName.Length-1] == Path.AltDirectorySeparatorChar)
				{
					Directory.CreateDirectory(fileName);
					fileName = Path.Combine(fileName, result.Hostname + "-" + DateTime.Now.ToString("yyyyMMddhhmmss") + ".json");
				}


				if (options.MinVerbosity(Verbosity.Standard))
					Console.WriteLine("[.] Writing to: {0}", fileName);


				FileInfo fileInfo = new FileInfo(fileName);
				if (!fileInfo.Directory.Exists) Directory.CreateDirectory(fileInfo.Directory.FullName);


				string serialised = null;
				try
				{
					serialised = result.Content.ToString();
				}
				catch (Exception)
				{
					Console.WriteLine("[!] Error: Unable to convert {0} to a saveable format", result.Hostname);
				}


				// Store it to the drive
				using (var filestream = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
				using (var file = new StreamWriter(filestream, System.Text.Encoding.Default, 4096))
				{
					file.WriteLine(serialised);
				}
			}
			catch (Exception)
			{
				Console.WriteLine("[!] Error: Unable to save {0} to {1}", result.Hostname, fileName);
			}
		}


		/// <summary>
		/// Obtain the grade from the scan
		/// </summary>
		/// <param name="content"></param>
		/// <returns></returns>
		private static string GetScore(JObject content)
		{
			int worstGrade = int.MaxValue;
			int bestGrade = 0;
			int epGrade;

			foreach (var endpoint in content["endpoints"])
			{
				if (endpoint["statusMessage"] != null && (string)endpoint["statusMessage"] == "Ready")
				{
					if (endpoint["grade"] != null)
					{
						epGrade = LetterToScore((string)endpoint["grade"]);
						if (epGrade > bestGrade) bestGrade = epGrade;
						if (epGrade < worstGrade) worstGrade = epGrade;
					}
				}
			}

			if (worstGrade == int.MaxValue) return String.Empty;
			if (bestGrade == worstGrade) return Grades[bestGrade];

			return Grades[bestGrade] + " - " + Grades[bestGrade];
		}


		/// <summary>
		/// Convert SLL Labs grade to numeric score
		/// </summary>
		/// <param name="gradeLetter"></param>
		/// <returns></returns>
		private static int LetterToScore(string gradeLetter)
		{
			for (int pos=0; pos < Grades.Length; pos++)
				if (gradeLetter == Grades[pos]) return pos;

			return int.MaxValue;		// Uh oh, unexpected grade
		}
	}
}
