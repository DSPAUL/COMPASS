﻿using COMPASS.Models;
using HtmlAgilityPack;
using System.Threading.Tasks;

namespace COMPASS.ViewModels.Sources
{
    public class GenericOnlineSourceViewModel : OnlineSourceViewModel
    {
        public override string ImportTitle => "Any URL";

        public override string ExampleURL => "https://";

        public override ImportSource Source => ImportSource.GenericURL;

        public override bool FetchCover(Codex codex) => false;
        public override async Task<Codex> SetMetaData(Codex codex)
        {
            ProgressChanged(new(LogEntry.MsgType.Info, $"Connecting to {InputURL}"));

            HtmlDocument doc = await ScrapeSite(InputURL);
            HtmlNode src = doc?.DocumentNode;

            if (src is null)
            {
                return codex;
            }

            ProgressChanged(new(LogEntry.MsgType.Info, "Fetching Metadata"));
            //Scrape metadata
            codex = SetWebScrapeHeaderMetadata(codex, src);

            MainViewModel.CollectionVM.FilterVM.PopulateMetaDataCollections();
            MainViewModel.CollectionVM.FilterVM.ReFilter();

            return codex;
        }
    }
}
