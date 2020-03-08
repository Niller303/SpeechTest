using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace SpeechTest.SpeechSynths {
	class MaryTTS : DefaultTTS {
		private HttpClient client;
		private MaryConfig config;
		private int id;
		private object ilock = new object();
		private ConcurrentDictionary<int, Task<MediaPlayer>> works;

		public MaryTTS() {
			id = 0;
			works = new ConcurrentDictionary<int, Task<MediaPlayer>>();

			client = new HttpClient();
			config = new MaryConfig();
			Program.ConfigLoad("marytts.json", ref config);
		}

		public override int Prep(string what) {
			int iid;
			lock (ilock) {
				iid = id++;
			}

			Task<MediaPlayer> tsk = new Task<MediaPlayer>(() => {
				var vars = new Dictionary<string, string>
					{
					{ "INPUT_TEXT", HttpUtility.UrlEncode(what) },
					{ "INPUT_TYPE", "TEXT" },
					{ "LOCALE", config.tts_locale },
					{ "VOICE", config.tts_voice },
					{ "OUTPUT_TYPE", "AUDIO" },
					{ "AUDIO", "WAVE" },
				};
				string args = string.Join("&", vars.Select((e) => $"{e.Key}={e.Value}").ToList());

				var request = (HttpWebRequest)WebRequest.Create($"http://{config.tts_address}:{config.tts_port}/process?{args}");
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
			});
			tsk.Start();
			works.TryAdd(iid, tsk);

			return iid;
		}

		public override void Speak(int what) {
			Task<MediaPlayer> text;
			works.TryRemove(what, out text);

			text.Wait();
			if (text.Result != null) {
				text.Result.Play();
			} else {
				Console.WriteLine("Failed to write text!");
			}
		}

		private class MaryConfig {
			public string tts_address = "localhost";
			public string tts_port = "59125";

			//MaryTTS
			public string tts_locale = "en_US";
			public string tts_voice = "cmu-slt";

			//en_US
			//{ "VOICE", "cmu-bdl" }, //Nicer
			//{ "VOICE", "cmu-rms" }, //Nicest
			//{ "VOICE", "cmu-slt" }, //Nice
			//en_GB
			//{ "VOICE", "dfki-obadiah" },
			//{ "VOICE", "dfki-poppy" },
			//{ "VOICE", "dfki-prudence" },
			//{ "VOICE", "dfki_spike" },

			//All have -hsmm versions 2
		}
	}
}
