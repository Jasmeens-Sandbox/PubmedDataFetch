using Microsoft.EntityFrameworkCore;
using PubmedDataFetch.Clients;
using PubmedDataFetch.Data;
using PubmedDataFetch.Models;
using System.Diagnostics;

namespace PubmedDataFetch
{
	internal class Program
	{
		static async Task Main(string[] args)
		{
            Console.Write("Search Key term: ");
			var KEY_TERM= Console.ReadLine();
			//var KEY_TERM = "Heart AI";
			//var KEY_TERM = "Cancer AI";
			//var KEY_TERM = "Cancer Machine";


			using var client = new HttpClient();
			var pubmed = new PubMedClient(client, "1c06dfe109ec053cc802fc5e62b92a6e4e09");

			var stopwatch = Stopwatch.StartNew();


			var pmids = (await pubmed.SearchPublicationIdsAsync(KEY_TERM, DateTime.Parse("2024-09-01"), DateTime.Now, 9999)).ToList();
			stopwatch.Stop();

			Console.WriteLine($"Fetched {pmids.Count} publication IDs in {stopwatch.Elapsed.TotalSeconds:N2} seconds.");


			using var dbContext = new PublicationDbContext();
			dbContext.Database.EnsureCreated();

			var existingIds = await dbContext.Publications
											 .Where(p => pmids.Contains(p.ProviderId))
											 .Select(p => p.ProviderId)
											 .ToListAsync();

			var newPmids = pmids.Except(existingIds).ToList();


			Console.WriteLine($"\nNew publications metadata to fetch: {newPmids.Count}");

			if (newPmids.Count > 0)
			{
				Console.WriteLine($"Fetching metadata...");

				//Fetch full publication details (title, abstract, authors, pub date)
				var newPublications = (await pubmed.FetchPublicationsAsync(newPmids)).ToList();
				Console.WriteLine("Metadata fetch complete.");

				//Normalize authors and insert into database
				foreach (var pub in newPublications)
				{
					// Skip already existing publications
					var exists = await dbContext.Publications
						.AnyAsync(p => p.Provider == pub.Provider && p.ProviderId == pub.ProviderId);
					if (exists) continue;

					var publicationEntity = new Publication
					{
						Provider = pub.Provider,
						ProviderId = pub.ProviderId,
						Title = pub.Title,
						Abstract = pub.Abstract,
						PublicationDate = pub.PublicationDate
					};

					for (int i = 0; i < pub.Authors.Count; i++)
					{
						publicationEntity.Authors.Add(new PublicationAuthor
						{
							Name = pub.Authors[i],
							AuthorOrder = i + 1
						});
					}

					await dbContext.Publications.AddAsync(publicationEntity);
				}

				if (newPublications.Count > 0)
					await dbContext.SaveChangesAsync();

				Console.WriteLine($"Inserted {newPublications.Count} new publications into the database.");
			}
			else
                Console.WriteLine("Our Database is up to date.");

            Console.WriteLine("press enter to quit...");
			Console.Read();
		}
	}
}

