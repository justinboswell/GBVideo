using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Xml.Serialization;

using Giantbomb.API;

namespace GBVideo
{
	public class VideoInstance
	{
		// Only the video info and the DownloadDirectory and deletion state need to be saved
		public VideoInfo VideoInfo { get; set; }
		public string DownloadDirectory { get; set; }
		public bool Deleted { get; set; }
		public bool Downloaded { get; set; }

		private WebClient Client = null;
		private int DownloadProgress = -1;

		#region Constructor
		// should only be called by serialization
		public VideoInstance()
		{
		}

		public VideoInstance(VideoInfo Info)
		{
			VideoInfo = Info;
			DownloadDirectory = Properties.Settings.Default.DownloadDirectory;
		}
		#endregion

		#region Properties
		[XmlIgnore]
		public int ID
		{
			get { return VideoInfo.ID; }
		}

		[XmlIgnore]
		public string Title
		{
			get { return VideoInfo.Name; }
		}

		[XmlIgnore]
		public int DownloadStatus
		{
			get
			{
				if (ExistsLocally)
					return 100;

				return (DownloadProgress != -1) ? DownloadProgress : 0;
			}
		}

		[XmlIgnore]
		public bool ExistsLocally
		{
			get
			{
				return File.Exists(LocalFilename) || File.Exists(FriendlyFilename);
			}
		}

		[XmlIgnore]
		public bool Downloading
		{
			get
			{
				return Client != null;
			}
		}

		[XmlIgnore]
		public string RemoteFilename
		{
			get
			{
				// Parse the URL, take the last segment, which will be the filename
				Uri HQVideoURL = new Uri(VideoInfo.HighQualityURL);
				string Filename = HQVideoURL.Segments[HQVideoURL.Segments.Length - 1];
				return Filename;
			}
		}

		[XmlIgnore]
		public string LocalFilename
		{
			get 
			{
				return Path.Combine(DownloadDirectory, RemoteFilename); 
			}
		}

		[XmlIgnore]
		public string FriendlyFilename
		{
			get
			{
				string SanitizedTitle = Title;
				foreach (char C in Path.GetInvalidFileNameChars())
				{
					SanitizedTitle = SanitizedTitle.Replace(C.ToString(), "");
				}

				return Path.Combine(DownloadDirectory, SanitizedTitle) + Path.GetExtension(RemoteFilename);
			}
		}

		[XmlIgnore]
		public string TempDownloadFilename
		{
			get { return LocalFilename + ".download"; }
		}

		[XmlIgnore]
		public DateTime LastViewed
		{
			get
			{
				if (File.Exists(FriendlyFilename))
				{
					return File.GetLastAccessTime(FriendlyFilename);
				}
				else if (File.Exists(LocalFilename))
				{
					return File.GetLastAccessTime(LocalFilename);
				}

				return DateTime.MinValue;
			}
		}
		#endregion

		#region Download
		public delegate void DownloadStateChangedHandler(VideoInstance Video, DownloadProgressChangedEventArgs e);
		public event DownloadStateChangedHandler DownloadStateChanged;
		private EventWaitHandle DownloadComplete = new EventWaitHandle(false, EventResetMode.AutoReset);

		public void Download()
		{
			Download(DownloadDirectory);
		}

		public void Download(string DestinationDirectory)
		{
			DownloadDirectory = DestinationDirectory;

			if (!ExistsLocally && !Downloading)
			{
				try
				{
					if (!Directory.Exists(DownloadDirectory))
						Directory.CreateDirectory(DownloadDirectory);

					Client = APIClient.CreateWebClient();
					Client.DownloadProgressChanged += new DownloadProgressChangedEventHandler(Client_DownloadProgressChanged);
					Client.DownloadFileCompleted += new System.ComponentModel.AsyncCompletedEventHandler(Client_DownloadFileCompleted);
					Client.DownloadFileAsync(new Uri(VideoInfo.HighQualityURL), TempDownloadFilename);
					DownloadComplete.WaitOne();
				}
				catch (System.Exception Ex)
				{
					Logger.Log("Exception while downloading {0}: {1}", VideoInfo.HighQualityURL, Ex);
				}
			}
			else if (ExistsLocally)
			{ // legacy data
				Downloaded = true;
			}
		}

		void Client_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
		{
			try
			{
				File.Move(TempDownloadFilename, FriendlyFilename);
			}
			catch (System.Exception Ex)
			{
				Logger.Log("Exception while moving {0} to {1}: {2}", TempDownloadFilename, FriendlyFilename, Ex);
			}

			DownloadProgress = -1;
			Client = null;
			DownloadComplete.Set();
			Downloaded = true;

			DownloadStateChanged(this, null);
		}

		void Client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
		{
			DownloadProgress = e.ProgressPercentage;
			DownloadStateChanged(this, e);
		}
		#endregion

		public void Delete()
		{
			if (File.Exists(FriendlyFilename))
			{
				File.Delete(FriendlyFilename);
			}
			if (File.Exists(LocalFilename))
			{
				File.Delete(LocalFilename);
			}

			Deleted = true;
		}
	}
}
