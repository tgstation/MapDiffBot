using System.IO;
using System.Web.Http;

namespace MapDiffBot.WebHook
{
	/// <summary>
	/// <see cref="ApiController"/> to access output files created by the map diff generator
	/// </summary>
	public class OperationsController : ApiController
	{
		/// <summary>
		/// Download a file from the /Operations data directory
		/// </summary>
		/// <param name="owner">The owner of the operation repository</param>
		/// <param name="repository">The name of the operation repository</param>
		/// <param name="pullRequestNumber">The number of the pull request in the operation</param>
		/// <param name="file">The name of the file in the operation</param>
		/// <returns>An <see cref="IHttpActionResult"/> pointing to the <paramref name="file"/></returns>
		[Route("Operations/{owner}/{repository}/{pullRequestNumber}/{file}")]
		[HttpGet]
		public IHttpActionResult GetFile(string owner, string repository, string pullRequestNumber, string file)
		{
			var realPath = Path.Combine(Application.DataDirectory, "Requests", "MapDiffs", "Operations", owner, repository, pullRequestNumber, file);
			if (!File.Exists(realPath))
				return NotFound();
			return new FileResult(realPath);
		}
	}
}