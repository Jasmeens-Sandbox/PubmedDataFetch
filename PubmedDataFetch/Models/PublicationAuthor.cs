namespace PubmedDataFetch.Models
{
	public class PublicationAuthor
	{
		public int Id { get; set; }
		public string Name { get; set; } = null!;
		public int AuthorOrder { get; set; }

		public int PublicationId { get; set; }
		public Publication Publication { get; set; } = null!;
	}
}
