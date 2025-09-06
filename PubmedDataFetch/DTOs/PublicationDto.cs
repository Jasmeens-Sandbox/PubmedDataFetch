namespace PubmedDataFetch.DTOs
{
	public class PublicationDto
	{
		public string Provider { get; set; } = null!;
		public string ProviderId { get; set; } = null!;
		public string Title { get; set; } = null!;
		public string Abstract { get; set; } = null!;
		public List<string> Authors { get; set; } = [];
		public DateTime PublicationDate { get; set; }
	}
}
