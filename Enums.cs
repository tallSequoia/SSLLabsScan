namespace TallSequoia.SSLLabsScan
{
	/// <summary>
	/// Output verbosity
	/// </summary>
	public enum Verbosity : int
	{
		/// <summary>
		/// No output
		/// </summary>
		None = 0,
		/// <summary>
		/// Only errors
		/// </summary>
		Errors = 1,
		/// <summary>
		/// Normal operation for most users
		/// </summary>
		Standard = 2,
		/// <summary>
		/// More detail for the inquisitive
		/// </summary>
		Detailed = 3,
		/// <summary>
		/// Every response from SSLLabs
		/// </summary>
		Responses = 4
	}

	/// <summary>
	/// Mode of caching that SSLLabs can use
	/// </summary>
	public enum CacheMode
	{
		/// <summary>
		/// Let the tool decide if the cache should be used
		/// </summary>
		Optimised,
		/// <summary>
		/// Always use the cache
		/// </summary>
		Always,
		/// <summary>
		/// Force a new scan
		/// </summary>
		Never
	}

	
	/// <summary>
	/// Level of detail of the output
	/// </summary>
	public enum DetailLevel
	{
		/// <summary>
		/// The (lowest) score
		/// </summary>
		Score,
		/// <summary>
		/// Normal verbosity
		/// </summary>
		Normal,
		/// <summary>
		/// All the details - akin to setting the "all" flag to "done"
		/// </summary>
		Detailed
	}


	/// <summary>
	/// Status of the query
	/// </summary>
	public enum QueryStatus
	{
		/// <summary>
		/// All good
		/// </summary>
		Success,

		/// <summary>
		/// Permitted run time exceeded
		/// </summary>
		Timeout,

		/// <summary>
		/// Undefined http status response from the web sever
		/// </summary>
		WebError,

		/// <summary>
		/// Server reprts too many requests from this client
		/// </summary>
		RateLimited,
		
		/// <summary>
		/// Server is generally overloaded
		/// </summary>
		Overloaded,

		/// <summary>
		/// Maintenance is underway
		/// </summary>
		Maintenance,


		ResponseError,
		ServiceError,
		InvalidHostname,
		HostError
	}
}
