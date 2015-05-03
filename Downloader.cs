using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using Giantbomb.API;

namespace GBVideo
{
	class Downloader
	{
		APIClient Client = new APIClient();
		Database DB = null;
		private object Lock = new Object();

		// Options
		static bool bListTypes = false;
		static bool bRunOnce = false;
		static bool bPreview = false;
		static string[] FilterTypes = null;

		void ParseCommandLine()
		{
			string[] Options = Environment.CommandLine.Split(' ');
			foreach (string Option in Options)
			{
				switch (Option.ToUpperInvariant())
				{
					case "-LISTTYPES":
						bListTypes = true;
						bRunOnce = true;
						break;

					case "-ONCE":
						bRunOnce = true;
						break;

					case "-PREVIEW":
						bPreview = true;
						bRunOnce = true;
						break;

					case "-VERBOSE":
						Logger.Verbose = true;
						break;

					case "-RESET":
						Properties.Settings.Default.Reset();
						Logger.Log("Settings restored to defaults");
						break;

					case "-CLEARCACHE":
						Database.Clear();
						break;
				}

				if (Option.StartsWith("-TYPES=", StringComparison.InvariantCultureIgnoreCase))
				{
					string Types = Option.Replace("-TYPES=", "");
					FilterTypes = Types.Split(',');
				}
			}
		}

		static void LoadSettings()
		{
			if (Properties.Settings.Default.DownloadDirectory.Length == 0)
			{
				string MyVideos = System.Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
				string DownloadDirectory = Path.Combine(MyVideos, "Giantbomb");
				Logger.Log("No download directory specified, setting download directory to {0}", DownloadDirectory);
				Properties.Settings.Default.DownloadDirectory = DownloadDirectory;
				Properties.Settings.Default.Save();
			}

			if (FilterTypes == null)
			{
				FilterTypes = new string[Properties.Settings.Default.VideoTypes.Count];
				Properties.Settings.Default.VideoTypes.CopyTo(FilterTypes, 0);
			}
		}

		void GetMetadataFromServer()
		{
			Logger.Log("Getting metadata from GiantBomb...");
			Client.GetVideoTypes();
			Logger.Log("Video types fetched");

			if (bListTypes)
			{
				foreach (KeyValuePair<string, int> VideoType in Client.VideoTypes)
				{
					Logger.Log("Type: [{0:d2}]: {1}", VideoType.Value, VideoType.Key);
				}
			}
		}

		void GetVideosOfType(string VideoTypeName)
		{
			int VideoType = -1;
			if (Client.VideoTypes.TryGetValue(VideoTypeName, out VideoType))
			{
				Logger.LogVerbose("Fetching {0} latest {1} videos", Properties.Settings.Default.QueryBatchSize, VideoTypeName);
				VideoInfo[] Infos = Client.GetLatestVideos(Properties.Settings.Default.QueryBatchSize, VideoType);
				if (Infos != null)
				{
					foreach (VideoInfo Info in Infos)
					{
						VideoInstance Video = new VideoInstance(Info);
						Video.DownloadDirectory = Path.Combine(Properties.Settings.Default.DownloadDirectory, VideoTypeName);
						if (DB.AddVideo(Video))
						{
							Logger.LogVerbose("Found {0}:{1}", Video.Title, Video.RemoteFilename);
						}
					}
				}
				Logger.LogVerbose("Done");
			}
			else
			{
				Logger.Log("Bad video type requested: {0}", VideoTypeName);
			}
		}

		public void Run()
		{
			ParseCommandLine();
			LoadSettings();
			LoadDatabase();

			GetMetadataFromServer();
			if (bListTypes)
				return;

			FetchVideos();
			CleanupOldVideos();

			if (!bRunOnce)
			{
				DateTime LastCheckTime = DateTime.Now;
				while (true)
				{
					TimeSpan TimeSinceLastCheck = DateTime.Now.Subtract(LastCheckTime);
					if (TimeSinceLastCheck.Minutes > 30)
					{
						LastCheckTime = DateTime.Now;
						FetchVideos();
						CleanupOldVideos();
					}

					Thread.Sleep(10);
				}
			}
		}

		void LoadDatabase()
		{
			DB = Database.Open();
		}

		void FetchVideos()
		{
			lock (Lock)
			{
				Logger.LogVerbose("Checking for new videos...");

				try
				{
					foreach (string VideoTypeName in FilterTypes)
					{
						GetVideosOfType(VideoTypeName);
					}
				}
				catch (Exception Ex)
				{
					Logger.Log("Error while fetching video info: {0}", Ex);
					Logger.Log("Will try again later");
				}

				if (!bPreview)
				{
					List<VideoInstance> VideosToDownload = DB.GetVideosToDownload();
					foreach (VideoInstance Video in VideosToDownload)
					{
						DownloadVideo(Video);
					}
				}
			}
		}

		void CleanupOldVideos()
		{
			return;
			Logger.LogVerbose("Checking for expired movies...");
			DeleteOldMovies(Properties.Settings.Default.DownloadDirectory);
			Logger.LogVerbose("Done");
		}

		DateTime DownloadStartTime;
		void DownloadVideo(VideoInstance Video)
		{
			Logger.Log("Downloading {0}:{1}", Video.Title, Video.RemoteFilename);
			Video.DownloadStateChanged += new VideoInstance.DownloadStateChangedHandler(Video_DownloadStateChanged);
			DownloadStartTime = DateTime.Now;
			Video.Download();
			DB.Save();
			Logger.Log("Download complete");
		}

		void Video_DownloadStateChanged(VideoInstance Video, DownloadProgressChangedEventArgs e)
		{
			lock (Video)
			{
				if (e != null)
				{
					const float MB = 1024.0f * 1024.0f;
					TimeSpan ElapsedTime = DateTime.Now - DownloadStartTime;
					float DownloadRate = (float)((e.BytesReceived / MB) / ElapsedTime.TotalSeconds);
					Console.CursorLeft = 0;
					Console.Write("Progress: {0}% {1:f2}/{2:f2}MB {3:f2}M/s", Video.DownloadStatus, e.BytesReceived / MB, e.TotalBytesToReceive / MB, DownloadRate);
					Console.CursorLeft = 0; // next write will overwrite this line
				}
			}
		}

		void DeleteOldMovies(string MovieDir)
		{
			List<VideoInstance> DownloadedVideos = DB.GetDownloadedVideos();
			List<VideoInstance> VideosToDelete = new List<VideoInstance>();
			foreach (VideoInstance Video in DownloadedVideos)
			{
				TimeSpan TimeSinceLastAccess = DateTime.Now - Video.LastViewed;
				if (TimeSinceLastAccess.Days > Properties.Settings.Default.DaysToKeepMovies)
				{
					Logger.Log("Found expired movie file {0}", Video.Title);
					VideosToDelete.Add(Video);
				}
			}
			
			if (!bPreview)
			{
				foreach (VideoInstance Video in VideosToDelete)
				{
					Logger.Log("Deleting {0}", Video.FriendlyFilename);
					try
					{
						Video.Delete();
						DB.Save();
					}
					catch (Exception e)
					{
						Logger.Log("Delete failed: {0}", e.Message);
					}
				}
			}
		}
	}
}
