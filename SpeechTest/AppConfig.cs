using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeechTest {
	class AppConfig {
		public string twitch_username = "mechannel";
		public string channel = "owochid";
		public string oauth = "oauth:dumbstuff";
		public string message_format = "{0} said {1} on {2}";
		public int maxmsg_len = -1;
		public string tts_name = "DefaultTTS";
		public List<string> mutes = new List<string>();
	}
}
