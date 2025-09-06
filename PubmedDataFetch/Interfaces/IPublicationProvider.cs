using PubmedDataFetch.DTOs;

namespace PubmedDataFetch.Interfaces
{
	public interface IPublicationProvider
	{
		Task<List<string>> SearchPublicationIdsAsync(string term, DateTime? startDate = null, DateTime? endDate = null, int maxResults = 9999);

		Task<IEnumerable<PublicationDto>> FetchPublicationsAsync(IEnumerable<string> ids);

		Task<IEnumerable<PublicationDto>> FetchPublicationsMetadataAsync(IEnumerable<string> ids);
	}
}
