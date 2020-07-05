# SSLLabsScan
Download SSLLabs scan results via command line.

Releases are for .NET Framework 4.6.1 (All supported Windows OS) and for dotnet core for Linux and MaxOS (latter untested).

Command line options:

 -t, --hostnames         Hostnames to scan (comma separated). e.g. rod.example.com,jane.example.com

  -i, --import            File lists to process (comma separated).

  -o, --output            File to save to. Use {hostname} and {date} for variables.

  --maxtries              (Default: 60) Maximum number of checks for updates.

  --pausetime             (Default: 4) Time to pause between checking for updates (s).

  -d, --adaptivedelay     (Default: true) Use feedback from SSLLabs to define delay

  --maxparallel           (Default: 1) Maximum number of parallel scans.

  -v, --verbosity         (Default: 2) Level of detail to show (None, Errors, Standard, Detailed or Responses)

  -p, --publish           (Default: false) Publish the results.

  -c, --cachemode         (Default: Optimised) Caching strategy. (Options: Optimised, Never, Always).

  -a, --maxage            (Default: 23) Maximum age of the cache results (hours).

  -l, --level             (Default: Normal) Level of detail. (Options: Score, Normal, Detailed)

  -m, --ignoremismatch    (Default: false) Proceed with assessments even when the server certificate doesn't match the assessment hostname?

  --help                  Display this help screen.

  --version               Display version information.


Examples:

1. Quick scan result. Do a scan with standard settings and view the results in the command line shell

  Windows:
    Scan.exe -t example.org
  Linux:
    dotnet Scan.dll -t example.org


2. Scan a list of hosts:

  Windows:
    Scan.exe -t http://example.org/ignored/folder/path,https://www.example.org,example.ie -i filelist.txt -v Standard --level Score -o "{hostname}.json"
  Linux:
    dotnet Scan.dll  -t http://example.org/ignored/folder/path,https://www.example.org,example.ie -i ~/filelist.txt -v Standard --level Score -o ~/scan/{hostname}.json

  Explanation:
    -t = different forms of hostname, which will be cleaned and deduplicated
    -i = external file(s) of hostnames, also cleaned and deduplicated
    -v = level of verbosity (what is shown in the command line shell)
    --level = level of detail from the output
    -o = store the JSON output to a file, which contains the hostname
