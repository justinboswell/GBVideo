using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Giantbomb.API;

namespace GBVideo
{
	public static class Program
	{
		static Downloader Downloader = new Downloader();

		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		public static void Main()
		{
			try
			{
				Downloader.Run();
			}
			catch (System.Exception Ex)
			{
				Logger.Log("Unhandled exception occurred: {0}", Ex);
			}
		}
	}
}
