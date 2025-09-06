namespace PubmedDataFetch.Models
{
	public class Publication
	{
		public int Id { get; set; }
		public string Provider { get; set; } = null!;
		public string ProviderId { get; set; } = null!;
		public string Title { get; set; } = null!;
		public string Abstract { get; set; } = null!;
		public DateTime PublicationDate { get; set; }

		public List<PublicationAuthor> Authors { get; set; } = [];
	}
}
