using PubmedDataFetch.DTOs;
using PubmedDataFetch.Interfaces;
using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace PubmedDataFetch.Clients
{
	public class PubMedClient(HttpClient httpClient, string apiKey) : IPublicationProvider
	{
		private readonly HttpClient _httpClient = httpClient;
		private readonly string _apiKey = apiKey;

		private DateTime ParsePubDate(XElement? pubDateElement)
		{
			if (pubDateElement == null) return DateTime.MinValue;

			var year = pubDateElement.Element("Year")?.Value;
			var month = pubDateElement.Element("Month")?.Value ?? "Jan";
			var day = pubDateElement.Element("Day")?.Value ?? "01";

			if (!int.TryParse(year, out var y)) return DateTime.MinValue;

			int m = MonthStringToNumber(month);
			int d = int.TryParse(day, out var dd) ? dd : 1;

			return new DateTime(y, m, d);

			static int MonthStringToNumber(string month)
			{
				return DateTime.ParseExact(month.Substring(0, 3), "MMM", System.Globalization.CultureInfo.InvariantCulture).Month;
			}
		}


		public async Task<List<string>> SearchPublicationIdsAsync(
			string term,
			DateTime? startDate = null,
			DateTime? endDate = null,
			int maxResults = 9999)
		{
			string query = $"({term}[ti] OR {term}[au]) AND Journal Article[pt]";

			if (startDate.HasValue || endDate.HasValue)
			{
				string start = startDate?.ToString("yyyy/MM/dd") ?? "1800/01/01";
				string end = endDate?.ToString("yyyy/MM/dd") ?? DateTime.Now.ToString("yyyy/MM/dd");
				query += $" AND ({start}[dp] : {end}[dp])";
			}

			var esearchUrl = $"https://eutils.ncbi.nlm.nih.gov/entrez/eutils/esearch.fcgi?" +
							 $"db=pubmed&term={Uri.EscapeDataString(query)}&retmax={maxResults}&retmode=json";

			if (!string.IsNullOrEmpty(_apiKey))
				esearchUrl += $"&api_key={_apiKey}";

			var esearchResponse = await _httpClient.GetFromJsonAsync<ESearchResponse>(esearchUrl);
			return esearchResponse?.EsSearchResult.IdList ?? [];
		}

		public async Task<IEnumerable<PublicationDto>> FetchPublicationsAsync(IEnumerable<string> ids)
		{
			var pmidList = ids.ToList();
			if (pmidList.Count == 0) return [];

			const int batchSize = 200;
			var batches = pmidList
				.Select((pmid, index) => new { pmid, index })
				.GroupBy(x => x.index / batchSize)
				.Select(g => g.Select(x => x.pmid).ToList())
				.ToList();

			var publications = new ConcurrentBag<PublicationDto>();
			int delayMs = string.IsNullOrEmpty(_apiKey) ? 350 : 110;

			foreach (var batch in batches)
			{
				var efetchUrl = $"https://eutils.ncbi.nlm.nih.gov/entrez/eutils/efetch.fcgi?" +
								$"db=pubmed&id={string.Join(",", batch)}&retmode=xml";

				if (!string.IsNullOrEmpty(_apiKey))
					efetchUrl += $"&api_key={_apiKey}";

				var responseXml = await _httpClient.GetStringAsync(efetchUrl);
				var doc = XDocument.Parse(responseXml);

				foreach (var p in doc.Descendants("PubmedArticle"))
				{
					var authors = p.Descendants("Author")
								   .Select(a => a.Element("LastName")?.Value + " " + a.Element("Initials")?.Value)
								   .Where(a => !string.IsNullOrEmpty(a))
								   .ToList();

					var pubDate = ParsePubDate(p.Element("MedlineCitation")?
											  .Element("Article")?
											  .Element("Journal")?
											  .Element("JournalIssue")?
											  .Element("PubDate"));

					publications.Add(new PublicationDto
					{
						Provider = "PubMed",
						ProviderId = p.Element("MedlineCitation")?.Element("PMID")?.Value ?? "",
						Title = p.Element("MedlineCitation")?.Element("Article")?.Element("ArticleTitle")?.Value ?? "",
						Abstract = string.Join(" ", p.Descendants("AbstractText").Select(a => a.Value)),
						Authors = authors,
						PublicationDate = pubDate
					});
				}

				// Respect rate limit
				await Task.Delay(delayMs);
			}

			return publications;
		}

		public async Task<IEnumerable<PublicationDto>> FetchPublicationsMetadataAsync(IEnumerable<string> ids)
		{
			var idList = string.Join(",", ids);
			var url = $"https://eutils.ncbi.nlm.nih.gov/entrez/eutils/esummary.fcgi?db=pubmed&id={idList}&retmode=json";

			if (!string.IsNullOrEmpty(_apiKey))
				url += $"&api_key={_apiKey}";

			var response = await _httpClient.GetFromJsonAsync<ESummaryResponse>(url);
			var result = new List<PublicationDto>();

			if (response?.Result == null) return result;

			foreach (var kvp in response.Result)
			{
				// Skip the "uids" property
				if (kvp.Key == "uids") continue;

				var item = kvp.Value;
				result.Add(new PublicationDto
				{
					ProviderId = item.Pmid,
					Title = item.Title,
					Authors = item.Authors ?? new List<string>(),
					PublicationDate = DateTime.TryParse(item.PubDate, out var dt) ? dt : DateTime.MinValue,
					Provider = "PubMed"
				});
			}

			return result;
		}
	}

	// ESearch JSON response
	public class ESearchResponse
	{
		[JsonPropertyName("esearchresult")]
		public ESearchResult EsSearchResult { get; set; } = new();
	}

	public class ESearchResult
	{
		[JsonPropertyName("count")]
		public string Count { get; set; } = "0";

		[JsonPropertyName("idlist")]
		public List<string> IdList { get; set; } = new();

		[JsonPropertyName("retmax")]
		public string RetMax { get; set; } = "0";

		[JsonPropertyName("retstart")]
		public string RetStart { get; set; } = "0";
	}

	public class ESummaryResponse
	{
		[JsonPropertyName("result")]
		public Dictionary<string, ESummaryItem> Result { get; set; } = new();
	}

	public class ESummaryItem
	{
		[JsonPropertyName("uid")]
		public string Pmid { get; set; } = "";

		[JsonPropertyName("title")]
		public string Title { get; set; } = "";

		[JsonPropertyName("pubdate")]
		public string PubDate { get; set; } = "";

		[JsonPropertyName("authors")]
		public List<string>? Authors { get; set; }
	}

}