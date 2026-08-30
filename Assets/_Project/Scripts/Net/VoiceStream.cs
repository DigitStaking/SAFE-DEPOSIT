// VoiceStream.cs  -  SAFE DEPOSIT
// Assets/_Project/Scripts/Net/VoiceStream.cs
// Goes on: the Player prefab.
//
// ====================================================================
// PHASE 4 STEP 10 - THE LAST PIECE: ACTUALLY SENDING IT.
//
// Everything around the voice has been working for several commits - the
// channel, the radio arbitration, the level meter, the distance and occlusion
// pipeline - and none of it moved a single sample between two machines. This
// does.
//
// WHY STEAM'S CAPTURE AND NOT UNITY'S
//
// Speech at 16kHz is about 256 kbit/s raw. That is not sendable, and Unity has
// no voice codec - it gives you samples and wishes you luck. Steam's GetVoice
// hands back the SAME speech at roughly 16 kbit/s, already compressed by a
// codec built for exactly this, and DecompressVoice turns it back. Sixteen
// times less traffic for free is the entire reason Steam Voice was chosen over
// paying for Dissonance.
//
// THE HONEST COST, AND IT IS THE MICROPHONE PICKER
//
// Steam captures from the WINDOWS DEFAULT recording device, and there is no
// API to point it at a different one. So the picker on the menu now chooses
// which mic the TEST METER listens to, not which one Steam sends. If the
// wrong device is being transmitted, it has to be changed in Windows sound
// settings or in Steam's own voice settings.
//
// That is worth saying out loud rather than hiding: the meter can be right
// while the transmission is wrong, and somebody would waste an hour on it.
//
// HOW IT TRAVELS
//
// Owner captures, sends to the host, host relays to everybody else. Not peer
// to peer - a four player crew is three sends instead of six, and the host is
// already the machine everything else defers to.
//
// The bytes arrive as an ArraySegment and go straight into the listener's
// VoiceMouth, which is where distance, concrete and the radio filter are
// applied. Nothing in this file knows or cares how far away anybody is.
// ====================================================================

using System.Collections.Generic;
using Steamworks;
using Unity.Netcode;
using UnityEngine;

public class VoiceStream : NetworkBehaviour
{
    /// <summary>
    /// Steam's own preferred rate. Asking for anything else makes it resample,
    /// which costs quality for no reason.
    /// </summary>
    static uint SampleRate => SteamBoot.Running
        ? SteamUser.GetVoiceOptimalSampleRate()
        : 24000u;

    const int CaptureBuffer = 8192;      // compressed bytes per poll, generous
    const int DecodeBuffer = 22050 * 2;  // one second of 16-bit PCM, generous

    readonly byte[] captured = new byte[CaptureBuffer];
    readonly byte[] decoded = new byte[DecodeBuffer];

    bool recording;

    /// <summary>Seconds left collecting what Steam already heard, after the
    /// key came up. See the note in Update.</summary>
    float drainLeft;

    const float DrainSeconds = 0.25f;

    // ---- playback ----
    AudioSource speaker;
    readonly Queue<float> pending = new Queue<float>();
    int clipPosition;

    void Awake()
    {
        var mouth = GetComponent<VoiceMouth>();
        speaker = mouth != null ? mouth.Source : GetComponent<AudioSource>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner) return;

        // ---- A STREAMING CLIP, NOT A SEQUENCE OF ONE-SHOTS ----
        //
        // Voice arrives in fragments a few times a second. Playing each as its
        // own clip produces an audible seam at every boundary - the click that
        // makes cheap voice chat sound cheap. A looping streaming clip is fed
        // continuously and never restarts, so there are no boundaries to hear.
        if (speaker == null) return;

        speaker.clip = AudioClip.Create("voice", (int)SampleRate, 1, (int)SampleRate,
                                        true, OnAudioRead, OnAudioSetPosition);
        speaker.loop = true;
        speaker.Play();
    }

    // ------------------------------------------------------------------
    // SPEAKING
    // ------------------------------------------------------------------

    void Update()
    {
        if (!IsOwner || !SteamBoot.Running) return;

        bool talking = VoiceTransmit.Local != VoiceTransmit.Channel.Silent;

        if (talking && !recording)
        {
            SteamUser.StartVoiceRecording();
            recording = true;
        }
        else if (!talking && recording && drainLeft <= 0f)
        {
            // ---- DRAIN BEFORE STOPPING ----
            //
            // Steam buffers what the microphone heard and hands it over in
            // chunks a few times a second, so at the instant a key comes up
            // there is always some speech captured and not yet collected.
            // Stopping immediately threw it away - which is why the END of
            // every sentence was missing.
            //
            // A fifth of a second of extra polling is enough to collect it,
            // and the recorder still stops: the mic is not left open, it is
            // just emptied first.
            drainLeft = DrainSeconds;
        }

        if (drainLeft > 0f)
        {
            drainLeft -= Time.deltaTime;

            if (drainLeft <= 0f && !talking)
            {
                SteamUser.StopVoiceRecording();
                recording = false;
            }
        }

        if (!recording) return;

        uint available;
        if (SteamUser.GetAvailableVoice(out available) != EVoiceResult.k_EVoiceResultOK)
            return;

        if (available == 0) return;

        uint written;
        var result = SteamUser.GetVoice(true, captured, CaptureBuffer, out written);

        if (result != EVoiceResult.k_EVoiceResultOK || written == 0) return;

        var frame = new byte[written];
        System.Array.Copy(captured, frame, (int)written);

        SendServerRpc(frame);
    }

    /// <summary>
    /// To the host, who relays. Three sends for a four-player crew instead of
    /// six, and the host is already the machine everything else defers to.
    /// </summary>
    [ServerRpc]
    void SendServerRpc(byte[] frame) => ReceiveClientRpc(frame);

    /// <summary>
    /// Arrives everywhere including the speaker's own machine, where OnNetworkSpawn
    /// created no clip and IsOwner drops it - nobody should hear themselves a
    /// fifth of a second late.
    /// </summary>
    [ClientRpc]
    void ReceiveClientRpc(byte[] frame)
    {
        if (IsOwner || !SteamBoot.Running || speaker == null) return;

        uint written;
        var r = SteamUser.DecompressVoice(frame, (uint)frame.Length,
                                          decoded, (uint)decoded.Length,
                                          out written, SampleRate);

        if (r != EVoiceResult.k_EVoiceResultOK || written == 0) return;

        // Steam gives signed 16-bit PCM; Unity wants floats from -1 to 1.
        lock (pending)
        {
            for (int i = 0; i + 1 < written; i += 2)
            {
                short s = (short)(decoded[i] | (decoded[i + 1] << 8));
                pending.Enqueue(s / 32768f);
            }

            // A HARD CAP ON THE BACKLOG.
            //
            // If frames arrive faster than they play - a burst after a stall -
            // the queue grows and every word gets later than the last, forever.
            // Dropping the oldest is worse than it sounds only in theory: half
            // a second of speech nobody has heard yet is not worth the delay it
            // would add to everything after it.
            int cap = (int)SampleRate / 2;
            while (pending.Count > cap) pending.Dequeue();
        }
    }

    // ------------------------------------------------------------------
    // HEARING
    // ------------------------------------------------------------------

    /// <summary>
    /// Called by Unity's audio thread, NOT the main thread - which is why the
    /// queue is locked. Touching Unity objects in here would be an error; all
    /// it does is copy floats.
    /// </summary>
    void OnAudioRead(float[] data)
    {
        lock (pending)
        {
            // ---- WAIT UNTIL THERE IS ENOUGH TO PLAY ----
            //
            // Frames arrive a few times a second and the audio thread asks for
            // samples continuously, so playing the instant anything shows up
            // means running dry between every frame - and a gap in the middle
            // of a word is heard as a missing consonant. "Some characters not
            // hearing", exactly.
            //
            // So playback holds until a short cushion exists, then plays
            // through. The cushion costs a fraction of a second of delay ONCE,
            // not per word, and it is the difference between speech and
            // stuttering.
            if (!flowing)
            {
                if (pending.Count < PrimeSamples)
                {
                    System.Array.Clear(data, 0, data.Length);
                    return;
                }
                flowing = true;
            }

            for (int i = 0; i < data.Length; i++)
            {
                if (pending.Count > 0) { data[i] = pending.Dequeue(); continue; }

                // Ran dry. Go quiet and re-prime rather than stuttering along
                // on an empty queue.
                data[i] = 0f;
                flowing = false;
            }
        }
    }

    bool flowing;

    /// <summary>
    /// How much has to be buffered before playback starts. A tenth of a
    /// second: enough to ride out normal jitter, short enough that nobody
    /// notices it in conversation.
    /// </summary>
    int PrimeSamples => (int)SampleRate / 10;

    void OnAudioSetPosition(int newPosition) => clipPosition = newPosition;

    public override void OnNetworkDespawn()
    {
        if (recording && SteamBoot.Running)
        {
            SteamUser.StopVoiceRecording();
            recording = false;
            drainLeft = 0f;
        }
    }
}
