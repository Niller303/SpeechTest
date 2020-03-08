using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net.Http;
using System.Net;
using System.Text.RegularExpressions;
using TwitchLib.Client;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Events;
using TwitchLib.Client.Extensions;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using System.Collections.Concurrent;
using System.Threading;
using System.Web;
using Newtonsoft.Json;
using SpeechTest.SpeechSynths;

namespace SpeechTest {
	class Program {
		//Username, Time of release
		private static Dictionary<string, long> timedout = new Dictionary<string, long>();
		//UserID, UserMessage
		private static ConcurrentQueue<ChatMessage> messages = new ConcurrentQueue<ChatMessage>();

		//Application config
		public static AppConfig config;
		private static DefaultTTS speech;

		//Speaking lock
		private static object SpeakingLock = new object();
		//Current user being spoken
		private static string SpeakingUser = "";

		private static long currentTime() {
			return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		}
		private static void RefreshTimeouts() {
			foreach (var entry in timedout.ToArray()) {
				if (entry.Value - Program.currentTime() <= 0) {
					timedout.Remove(entry.Key);
				}
			}
		}
		private static int GetTTS(ChatMessage m) {
			RefreshTimeouts();
			if (timedout.Keys.Contains(m.Username)) {
				return -1;
			}
			if (config.mutes.Contains(m.Username)) { //TODO: Use UserID
				return -1;
			}
			if (config.maxmsg_len != -1 && m.Message.Length > config.maxmsg_len) {
				return -1;
			}
			if (m.Username == config.twitch_username) {
				return -1;
			}

			var formatted = string.Format(config.message_format, m.Username, m.Message, m.Platform);
			return speech.Prep(formatted);
		}

		private class ChatMessage {
			public string Username { get; private set; }
			public string Message { get; private set; }
			public string Platform { get; private set; }
			public string UserID { get; private set; }
			public int Player { get; private set; }
			public ChatMessage(string Username, string Message, string Platform, string UserID="-1") {
				this.Username = Username;
				this.Message = Message;
				this.Platform = Platform;
				this.UserID = UserID;
				this.Player = GetTTS(this);
			}
		}

		private static TwitchClient client;
		class Bot {
			public Bot() {
				ConnectionCredentials credentials = new ConnectionCredentials(config.twitch_username.ToLower(), config.oauth);
				var clientOptions = new ClientOptions {
					MessagesAllowedInPeriod = 750,
					ThrottlingPeriod = TimeSpan.FromSeconds(30)
				};
				WebSocketClient customClient = new WebSocketClient(clientOptions);
				client = new TwitchClient(customClient);
				client.Initialize(credentials, config.channel);

				client.OnFailureToReceiveJoinConfirmation += (sender, e) => Console.WriteLine($"Failed to join channel: {config.channel}!");
				client.OnError += (sender, e) => Console.WriteLine(e.Exception);
				client.OnIncorrectLogin += (sender, e) => Console.WriteLine(e.Exception);
				client.OnJoinedChannel += (sender, e) => Console.WriteLine($"Joined channel: {e.Channel}!");
#if DEBUG
				//client.OnLog += (sender, e) => Console.WriteLine(e.Data);
#endif
				client.OnMessageReceived += Client_OnMessageReceived;
				client.OnUserTimedout += Client_OnUserTimedOut;

				client.Connect();
			}
			
			private void Client_OnUserTimedOut(object sender, OnUserTimedoutArgs e) {
				timedout.Add(e.UserTimeout.Username, Program.currentTime() + (long)e.UserTimeout.TimeoutDuration);

				if (e.UserTimeout.Username == SpeakingUser) {
					if (Monitor.TryEnter(SpeakingLock)) { //We are not speaking
						Monitor.Exit(SpeakingLock);
					} else { //We are speaking
						Console.WriteLine("Interrupting TTS...");
						Thread.CurrentThread.Interrupt();
					}
				}
			}

			private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e) {
				if (e.ChatMessage.Message[0] == '!') {
					string msg = e.ChatMessage.Message;
					
					if (msg.StartsWith("!mute") && (e.ChatMessage.IsBroadcaster || e.ChatMessage.IsModerator)) {
						string who = msg.Replace("!mute ", "");
						config.mutes.Add(who);
						Console.WriteLine($"Muted {who}");
						ConfigSave("app.json", config);
					}
				} else {
					if (e.ChatMessage.Username == "RestreamBot") {
						Match reg = Regex.Match(e.ChatMessage.Message, "\\[(\\w+): (\\w+)\\] (.+)");
						if (reg.Success) {
							string platform = reg.Groups[1].Value;
							string name = reg.Groups[2].Value;
							string message = reg.Groups[3].Value;
							messages.Enqueue(new ChatMessage(name, message, platform));
						}
					} else {
						messages.Enqueue(new ChatMessage(e.ChatMessage.Username, e.ChatMessage.Message, "Twitch", e.ChatMessage.UserId));
					}
				}
			}
		}
		
		private static void Speech() {
			while (true) {
				while (messages.Count > 0) {
					messages.TryDequeue(out ChatMessage m);
					
					if (timedout.Keys.Contains(m.Username)) {
						continue;
					}
					
					if (m.Platform == "Twitch" && m.UserID != "-1") {
						//Special userid specific rules go here
						//Other platforms are untrusted because we dont know the id
					}

					SpeakingUser = m.Username;
					lock (SpeakingLock) {
						try {
							Console.WriteLine($"{m.Username} said: {m.Message} from: {m.Platform}");
							if (m.Player != -1) {
								speech.Speak(m.Player);
							}
						} catch (ThreadInterruptedException) {}
					}
				}

				Thread.Sleep(1 * 1000);
			}
		}

		public static void ConfigLoad<T>(string file_path, ref T config) where T : new() {
			if (!File.Exists(file_path)) {
				ConfigSave(file_path, config);
			} else {
				using (var file = File.OpenRead(file_path))
				using (var reader = new StreamReader(file)) {
					config = JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
				}
			}
		}
		public static void ConfigSave<T>(string file_path, T config) {
			using (var file = File.Create(file_path))
			using (var writer = new StreamWriter(file)) {
				writer.Write(JsonConvert.SerializeObject(config));
			}
		}

		static void Main(string[] args) {
			Console.WriteLine("Started!");

			ConfigLoad("app.json", ref config);
			switch (config.tts_name) {
				case "MaryTTS":
					Console.WriteLine($"Using TTS: {config.tts_name}");
					speech = new MaryTTS();
					break;
				case "MozillaTTS":
					Console.WriteLine($"Using TTS: {config.tts_name}");
					speech = new MozillaTTS();
					break;
				case "MSTTS":
				default:
					Console.WriteLine($"Using TTS: MSTTS");
					speech = new MSTTS();
					break;
			}

			Bot bot = new Bot();
			Speech();
			
			Console.WriteLine("Stopped!");
			Console.ReadLine();
		}
	}
}
