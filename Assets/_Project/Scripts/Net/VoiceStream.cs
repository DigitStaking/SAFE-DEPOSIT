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
        else if (!talking && recording)
        {
            // Stopped the moment the key comes up. Steam keeps capturing until
            // told otherwise, and a game that leaves the recorder running is
            // one people are right to be suspicious of.
            SteamUser.StopVoiceRecording();
            recording = false;
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
            for (int i = 0; i < data.Length; i++)
                data[i] = pending.Count > 0 ? pending.Dequeue() : 0f;
        }
    }

    void OnAudioSetPosition(int newPosition) => clipPosition = newPosition;

    public override void OnNetworkDespawn()
    {
        if (recording && SteamBoot.Running)
        {
            SteamUser.StopVoiceRecording();
            recording = false;
        }
    }
}
