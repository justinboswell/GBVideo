using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;

using System.Xml.Serialization;
using System.IO;

using GBVideo;

namespace Giantbomb.API
{
	//[XmlType("response")]
	public class Response
	{
		[XmlElement(ElementName = "status_code")]
		public int Status { get; set; }

		[XmlElement(ElementName = "error")]
		public string Error { get; set; }

		[XmlElement(ElementName = "number_of_page_results")]
		public int ResultCount { get; set; }

		[XmlElement(ElementName = "number_of_total_results")]
		public int TotalResultCount { get; set; }

		[XmlElement(ElementName = "limit")]
		public int Limit { get; set; }

		[XmlElement(ElementName = "offset")]
		public int Offset { get; set; }
	}

	#region Video
	[XmlType("video")]
	public class VideoInfo
	{
		[XmlElement(ElementName = "id")]
		public int ID { get; set; }

		[XmlElement(ElementName = "low_url")]
		public string LowQualityURL { get; set; }

		[XmlElement(ElementName = "high_url")]
		public string HighQualityURL { get; set; }

		[XmlElement(ElementName = "hd_url")]
		public string HDQualityURL { get; set; }

		[XmlElement(ElementName = "name")]
		public string Name { get; set; }
	}

	[XmlType("response")]
	public class VideoResponse : Response
	{
		[XmlArray(ElementName = "results")]
		public VideoInfo[] Videos { get; set; }
	}
	#endregion

	#region VideoTypes
	[XmlType("video_type")]
	public class VideoType
	{
		[XmlElement(ElementName = "id")]
		public int ID { get; set; }

		[XmlElement(ElementName = "name")]
		public string Name { get; set; }

		[XmlElement(ElementName = "deck")]
		public string Description { get; set; }
	}

	[XmlType("response")]
	public class VideoTypesResponse : Response
	{
		[XmlArray(ElementName = "results")]
		public VideoType[] Types { get; set; }
	}
	#endregion

	class APIClient
	{
		private WebClient Client = null;

		public Dictionary<string,int> VideoTypes { get; private set; }

		public delegate void RequestCompleteNotification();
		public delegate void VideoRequestNotification(VideoInfo[] Videos);

		public APIClient()
		{
			ResetClient();
		}

		#region HTTP
		public static WebClient CreateWebClient()
		{
			WebClient Client = new WebClient();
			Client.BaseAddress = "http://api.giantbomb.com/";
			Client.QueryString["api_key"] = "08786ab003fdb3e663de0a849a66075a2b845063";

			return Client;
		}

		private T ParseResponse<T>(string XmlData) where T : class
		{
			if (XmlData != null)
			{
				try
				{
					StringReader Reader = new StringReader(XmlData);
					XmlSerializer Serializer = new XmlSerializer(typeof(T));
					T Result = (T)Serializer.Deserialize(Reader);
					return Result;
				}
				catch (System.Exception Ex)
				{
					Logger.Log("Exception: {0}", Ex);
				}
			}

			return null;
		}

		private void ResetClient()
		{
			Client = CreateWebClient();
		}

		private string FetchResults(string Resource, Dictionary<string, string> Params)
		{
			try
			{
				ResetClient();

				// Add params to query
				if (Params != null)
				{
					foreach (KeyValuePair<string, string> Pair in Params)
					{
						Client.QueryString.Add(Pair.Key, Pair.Value);
					}
				}

				Uri Address = new Uri(@"/" + Resource + @"/", UriKind.Relative);
				return Client.DownloadString(Address);
			}
			catch (Exception Ex)
			{
				Logger.LogVerbose("Error during FetchResults: {0}", Ex);
			}

			return null;
		}
		#endregion

		public int GetTotalVideoCount()
		{
			return GetTotalVideoCount(-1);
		}

		public int GetTotalVideoCount(int VideoType)
		{
			Dictionary<string, string> Params = new Dictionary<string, string>()
			{
			    {"limit", "0"},
			};
			if (VideoType != -1)
				Params.Add("video_type", VideoType.ToString());

			string Xml = FetchResults("videos", Params);
			VideoResponse Result = ParseResponse<VideoResponse>(Xml);
			return Result.TotalResultCount;
		}

		public VideoInfo[] GetLatestVideos(int Count, int VideoType)
		{
			try
			{
				int TotalVideos = GetTotalVideoCount(VideoType);
				int Offset = TotalVideos - Count;
				Dictionary<string, string> Params = new Dictionary<string, string>()
				{
					{"sort", "publish_date"},
					{"limit", Count.ToString() },
					{"offset", Offset.ToString() },
					{"video_type", VideoType.ToString() },
				};
				string Xml = FetchResults("videos", Params);
				VideoResponse Result = ParseResponse<VideoResponse>(Xml);
				return Result.Videos;
			}
			catch (Exception Ex)
			{
				Logger.LogVerbose("Error during GetLatestVideos: {0}", Ex);
			}

			return null;
		}

		public Dictionary<string, int> GetVideoTypes()
		{
			if (VideoTypes == null)
			{
				string Xml = FetchResults("video_types", null);
				VideoTypesResponse Result = ParseResponse<VideoTypesResponse>(Xml);

				VideoTypes = new Dictionary<string, int>();
				foreach (VideoType VidType in Result.Types)
				{
					VideoTypes.Add(VidType.Name, VidType.ID);
				}
			}

			return VideoTypes;
		}
	}
}
