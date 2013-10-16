
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

using Giantbomb.API;

namespace GBVideo
{
	public class Database
	{
		public List<VideoInstance> Videos = new List<VideoInstance>();

		private static string CacheFilename
		{
			get
			{
				return Path.Combine(Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "GBVideo"), "cache.xml");
			}
		}

		public static Database Open()
		{
			if (File.Exists(CacheFilename))
			{
				try
				{
					FileStream CacheFileStream = new FileStream(CacheFilename, FileMode.Open);
					XmlSerializer Serializer = new XmlSerializer(typeof(Database));
					Database DiskCache = Serializer.Deserialize(CacheFileStream) as Database;
					if (DiskCache != null)
						return DiskCache;
				}
				catch (Exception Ex)
				{
					Logger.Log("Exception occurred while trying to read cache from {0}: {1}", CacheFilename, Ex);
				}
			}

			// Either the file didn't exist or something fucked up, new clear cache
			return new Database();
		}

		public static void Clear()
		{
			if (File.Exists(CacheFilename))
			{
				try
				{
					File.Delete(CacheFilename);
					Logger.Log("Video cache cleared");
				}
				catch (Exception Ex)
				{
					Logger.Log("Exception occurred while trying to delete cache file {0}: {1}", CacheFilename, Ex);
				}
			}
		}

		public void Save()
		{
			lock (this)
			{
				try
				{
					FileStream CacheFileStream = new FileStream(CacheFilename + ".tmp", FileMode.Create);
					XmlSerializer Serializer = new XmlSerializer(typeof(Database));
					Serializer.Serialize(CacheFileStream, this);
					CacheFileStream.Close();
					File.Delete(CacheFilename);
					File.Move(CacheFilename + ".tmp", CacheFilename);
				}
				catch (Exception Ex)
				{
					Logger.Log("Exception occurred while trying to write cache to {0}: {1}", CacheFilename, Ex);
				}
			}
		}

		public VideoInstance GetVideoByID(int VideoID)
		{
			var QueryResult = (
				from V in Videos
				where V.ID == VideoID
				select V);

			try
			{
				return QueryResult.ElementAt(0);
			}
			catch (ArgumentOutOfRangeException)
			{
				return null;
			}
		}

		public bool AddVideo(VideoInstance Video)
		{
			if (GetVideoByID(Video.ID) == null)
			{
				Videos.Add(Video);
				Save();
				return true;
			}

			return false;
		}

		public List<VideoInstance> GetVideosToDownload()
		{
			var QueryResult = (
				from V in Videos
				where V.Downloaded == false && V.Deleted == false
				select V
			);

			return QueryResult.ToList<VideoInstance>();
		}

		public List<VideoInstance> GetDownloadedVideos()
		{
			var QueryResult = (
				from V in Videos
				where V.ExistsLocally == true && V.Deleted == false
				select V);

			return QueryResult.ToList<VideoInstance>();
		}
	};
}
