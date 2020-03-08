using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SpeechTest.SpeechSynths {
	class DefaultTTS {
		public DefaultTTS() {}
		public virtual int Prep(string text) { return -1; }
		public virtual void Speak(int what) {}
	}
}
