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

namespace SpeechTest {
	class Program {
		//Username, Time of release
		private static Dictionary<string, long> timedout = new Dictionary<string, long>();
		//UserID, UserMessage
		private static ConcurrentQueue<ChatMessage> messages = new ConcurrentQueue<ChatMessage>();
		public static AppConfig config;

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

		private class ChatMessage {
			public string Username { get; private set; }
			public string Message { get; private set; }
			public string Platform { get; private set; }
			public ChatMessage(string Username, string Message, string Platform) {
				this.Username = Username;
				this.Message = Message;
				this.Platform = Platform;
			}
		}

		class Bot {
			TwitchClient client;

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

				if (e.UserTimeout.Username == _currentUser) {
					if (Monitor.TryEnter(_lock)) { //We are not speaking
						Monitor.Exit(_lock);
					} else { //We are speaking
						Console.WriteLine("Interrupting TTS...");
						Thread.CurrentThread.Interrupt();
					}
				}
			}

			private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e) {
				if (e.ChatMessage.Username == "RestreamBot") {
					Match reg = Regex.Match(e.ChatMessage.Message, "\\[(\\w+): (\\w+)\\] (.+)");
					if (reg.Success) {
						string platform = reg.Groups[1].Value;
						string name = reg.Groups[2].Value;
						string message = reg.Groups[3].Value;
						messages.Enqueue(new ChatMessage(name, message, platform));
					}
				} else {
					messages.Enqueue(new ChatMessage(e.ChatMessage.Username, e.ChatMessage.Message, "Twitch"));
				}
			}
		}
		
		private static object _lock = new object();
		private static string _currentUser = "";
		private static void Speech() {
			while (true) {
				while (messages.Count > 0) {
					messages.TryDequeue(out ChatMessage m);

					RefreshTimeouts();
					if (timedout.Keys.Contains(m.Username)) {
						continue;
					}
					
					_currentUser = m.Username;
					lock (_lock) {
						try {
							/*if (m.Message.Length > 200) {
								continue;
							}*/
							if (m.Message[0] == '!') {
								continue;
							}

							Console.WriteLine($"{m.Username} said: {m.Message} from: {m.Platform}");
							var player = TTS.Speech(string.Format(config.message_format, m.Username, m.Message, m.Platform));
							if (player != null) {
								player.Play();
							}
						} catch (ThreadInterruptedException) {}
					}
				}

				Thread.Sleep(1 * 1000);
			}
		}

		static void Main(string[] args) {
			Console.WriteLine("Started!");

			if (!File.Exists("app.json")) {
				using (var file = File.Create("app.json"))
				using (var writer = new StreamWriter(file)) {
					config = new AppConfig();
					writer.Write(JsonConvert.SerializeObject(config));
				}
				Console.WriteLine("I was unable to find app.json, so i created one!\nPlease edit it to match your channel, oauth, and msgformat");
			} else {
				using (var file = File.OpenRead("app.json"))
				using (var reader = new StreamReader(file)) {
					config = JsonConvert.DeserializeObject<AppConfig>(reader.ReadToEnd());
				}
				
				Bot bot = new Bot();
				Speech();
			}
			Console.WriteLine("Stopped!");
			Console.ReadLine();
		}
	}
}
