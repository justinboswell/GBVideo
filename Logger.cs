
using System;
using System.Diagnostics;

namespace GBVideo
{
	public class Logger
	{
		public static bool Verbose = false;

		static public void Log(string Format, params object[] Args)
		{
			// Replace exceptions with their contents
			for (int Idx = 0; Idx < Args.Length; ++Idx)
			{
				Exception Ex = Args[Idx] as Exception;
				if (Ex != null)
				{
					Args[Idx] = string.Format("Exception: {0}\n{1}", Ex.Message, Ex.StackTrace);
				}
			}

			Console.Write("[" + DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss") + "] ");
			Console.WriteLine(Format, Args);
			Debug.WriteLine(Format, Args);
		}

		static public void LogVerbose(string Format, params object[] Args)
		{
			if (Verbose)
			{
				Log(Format, Args);
			}
		}
	};
}