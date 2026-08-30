// VoiceMic.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Net/VoiceMic.cs
// Goes on: nothing. Starts itself.
//
// ====================================================================
// PHASE 4 STEP 10 - IS THERE A MICROPHONE, AND IS IT HEARING ANYTHING.
//
// Two questions that were being guessed at:
//
//   "when i click V i can't talk and there is nothing means that i'm talking"
//   "check if the game detecte mics"
//
// Both are the same missing thing. Holding a key that produces no sound, no
// icon and no log entry is indistinguishable from a key that does nothing at
// all - which is why V felt like it was doing something else.
//
// SO IT OPENS THE MICROPHONE AND MEASURES IT.
//
// Unity's Microphone class, not Steam's: it needs no Steam, works in the
// editor, and answers the question directly. What it gives is a level, which
// is exactly what an indicator needs - a mic icon that only appears is a
// promise, and a bar that moves when you speak is proof.
//
// This is also the first half of the capture layer. Steam Voice will compress
// and send; this is the part that gets a signal in the first place, and having
// it standing on its own means a broken microphone can be told apart from a
// broken network without unpicking both.
//
// RECORDING ONLY WHILE TRANSMITTING
//
// The mic opens when you press a key and closes when you let go. A game that
// holds an open microphone the whole time it is running is a game people are
// right to be suspicious of, and push-to-talk makes that easy to promise
// honestly.
// ====================================================================

using UnityEngine;

public static class VoiceMic
{
    /// <summary>0 to 1, roughly. What the microphone is hearing right now.</summary>
    public static float Level { get; private set; }

    /// <summary>Null when there is no microphone at all.</summary>
    public static string Device { get; private set; }

    public static bool HasMic => !string.IsNullOrEmpty(Device);

    const int SampleRate = 16000;      // plenty for speech, and what voice codecs want
    const int ClipSeconds = 1;
    const int Window = 512;            // samples measured per frame

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var devices = Microphone.devices;

        if (devices == null || devices.Length == 0)
        {
            Debug.LogWarning("[Voice] NO MICROPHONE FOUND. Push-to-talk will " +
                             "work as a mechanic - the radio, the channel, the " +
                             "one-at-a-time rule - but nothing will be heard. " +
                             "Check Windows sound settings and that Unity has " +
                             "microphone permission.");
            return;
        }

        Device = devices[0];

        var sb = new System.Text.StringBuilder();
        sb.Append("[Voice] microphone: ").Append(Device);
        if (devices.Length > 1)
            sb.Append("  (").Append(devices.Length).Append(" available, using the first)");
        Debug.Log(sb.ToString());

        var go = new GameObject("~VoiceMic");
        go.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(go);
        go.AddComponent<Runner>();
    }

    class Runner : MonoBehaviour
    {
        AudioClip clip;
        readonly float[] window = new float[Window];
        bool recording;

        void Update()
        {
            bool wantOn = VoiceTransmit.Local != VoiceTransmit.Channel.Silent;

            if (wantOn && !recording) Open();
            else if (!wantOn && recording) Close();

            Level = recording ? Measure() : 0f;
        }

        void Open()
        {
            clip = Microphone.Start(Device, true, ClipSeconds, SampleRate);
            recording = true;
        }

        void Close()
        {
            Microphone.End(Device);
            clip = null;
            recording = false;
            Level = 0f;
        }

        /// <summary>
        /// RMS of the most recent window, which is the honest measure of "how
        /// loud is this" - a peak reading spikes on a single click and reads as
        /// speech, and an average of absolute values under-reads quiet talking.
        /// </summary>
        float Measure()
        {
            if (clip == null) return 0f;

            int head = Microphone.GetPosition(Device) - Window;
            if (head < 0) return Level;      // not a full window yet; hold

            if (!clip.GetData(window, head)) return Level;

            float sum = 0f;
            for (int i = 0; i < Window; i++) sum += window[i] * window[i];

            float rms = Mathf.Sqrt(sum / Window);

            // Speech sits low in a linear scale, so a raw RMS bar barely moves
            // and reads as broken. Scaled so normal talking fills about half
            // and shouting fills it - which is what the meter is for.
            float shown = Mathf.Clamp01(rms * 12f);

            // Rises fast, falls slow: a meter that drops instantly between
            // syllables flickers and looks like a fault.
            return shown > Level ? shown : Mathf.Lerp(Level, shown, 8f * Time.deltaTime);
        }

        void OnApplicationQuit()
        {
            if (recording) Close();
        }
    }
}
