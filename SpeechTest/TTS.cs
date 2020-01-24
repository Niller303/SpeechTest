using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace SpeechTest {
	class TTS {
		private static HttpClient client = new HttpClient();

		public static MediaPlayer Speech(string what) {
			var vars = new Dictionary<string, string>
			{
				{ "INPUT_TEXT", HttpUtility.UrlEncode(what) },
				{ "INPUT_TYPE", "TEXT" },
				{ "LOCALE", Program.config.tts_locale },
				{ "VOICE", Program.config.tts_voice },
				{ "OUTPUT_TYPE", "AUDIO" },
				{ "AUDIO", "WAVE" },
			};
			string args = string.Join("&", vars.Select((e) => $"{e.Key}={e.Value}").ToList());

			var request = (HttpWebRequest)WebRequest.Create($"http://{Program.config.tts_address}:{Program.config.tts_port}/process?{args}");
			HttpWebResponse response;

			try {
				response = (HttpWebResponse)request.GetResponse();
			} catch (WebException) {
				Console.WriteLine("Error connecting to TTS server!");
				return null;
			}

			if (response.ContentType != "audio/x-wav") {
				Console.WriteLine("Invalid content type!");
				return null;
			}

			using (var stream = response.GetResponseStream()) {
				return new MediaPlayer(stream);
			}
		}
	}
}
