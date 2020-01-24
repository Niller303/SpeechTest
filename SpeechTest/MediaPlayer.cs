using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeechTest {
	class MediaPlayer {
		static System.Media.SoundPlayer soundPlayer;

		public MediaPlayer(byte[] buffer) {
			var memoryStream = new MemoryStream(buffer, true);
			soundPlayer = new System.Media.SoundPlayer(memoryStream);
		}
		public MediaPlayer(Stream stream) : this(ReadFully(stream)) {}
		
		private static byte[] ReadFully(Stream input) {
			using (MemoryStream ms = new MemoryStream()) {
				input.CopyTo(ms);
				return ms.ToArray();
			}
		}

		public void Play() {
			soundPlayer.PlaySync();
		}

		public void Play(byte[] buffer) {
			soundPlayer.Stream.Seek(0, SeekOrigin.Begin);
			soundPlayer.Stream.Write(buffer, 0, buffer.Length);
			soundPlayer.Play();
		}
	}
}
