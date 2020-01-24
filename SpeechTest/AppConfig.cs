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

		public string tts_address = "localhost";
		public string tts_port = "59125";
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
