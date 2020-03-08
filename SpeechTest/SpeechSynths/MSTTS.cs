using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;

namespace SpeechTest.SpeechSynths {
	class MSTTS : DefaultTTS{
		private SpeechSynthesizer synth;
		private int id;
		private ConcurrentDictionary<int, string> works;

		public MSTTS() {
			id = 0;
			works = new ConcurrentDictionary<int, string>();

			// Initialize a new instance of the SpeechSynthesizer.  
			synth = new SpeechSynthesizer();

			// Configure the audio output.
			synth.SetOutputToDefaultAudioDevice();
			//TODO: add config voice
		}

		public override int Prep(string text) {
			works.TryAdd(id, text);
			return id++;
		}

		public override void Speak(int what) {
			string text;
			works.TryRemove(what, out text);
			synth.Speak(text);
		}
	}
}
