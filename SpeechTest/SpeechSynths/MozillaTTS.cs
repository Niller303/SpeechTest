using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace SpeechTest.SpeechSynths {
	class MozillaTTS : DefaultTTS {
		private HttpClient client = new HttpClient();
		private MozillaConfig config;
		private int id;
		private object ilock = new object();
		private ConcurrentDictionary<int, Task<MediaPlayer>> works;

		public MozillaTTS() {
			id = 0;
			works = new ConcurrentDictionary<int, Task<MediaPlayer>>();

			client = new HttpClient();
			config = new MozillaConfig();
			Program.ConfigLoad("mozillatts.json", ref config);
		}
		
		public override int Prep(string what) {
			int iid;
			lock (ilock) {
				iid = id++;
			}

			Task<MediaPlayer> tsk = new Task<MediaPlayer>(() => {
				var request = (HttpWebRequest)WebRequest.Create($"http://{config.tts_address}:{config.tts_port}/api/tts?text={HttpUtility.UrlEncode(what)}");
				HttpWebResponse response;

				try {
					response = (HttpWebResponse)request.GetResponse();
				} catch (WebException) {
					Console.WriteLine("Error connecting to TTS server!");
					return null;
				}

				if (response.ContentType != "audio/wav") {
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

		private class MozillaConfig {
			public string tts_address = "localhost"; //Please dont unless you have a 64 core AMD Epyc Rome cpu thanks
			public string tts_port = "5002";
		}
	}
}
