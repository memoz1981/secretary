// Captures the microphone, streams mono PCM16 over the /voice/live-call WebSocket, and plays
// the agent's audio back. Binary frames are raw PCM16 audio in both directions; Text frames from
// the server are control signals.
//
// Dials any of the four pipelines, because the only honest way to compare them is back to back
// on the same microphone and the same database. See lib/pipelines.ts for what each one is.
//
// The sample rate is NOT cosmetic. Send 24 kHz audio to a 16 kHz pipeline and the agent hears a
// chipmunk — and it will still transcribe *something*, which is worse than failing outright
// because the call appears to work while every word is wrong.

import type { CallPipeline } from "@/shared/api/types";

interface PipelineProfile {
  sampleRate: number;
  /** ~100 ms of audio per WebSocket frame — responsive for turn detection, without flooding
   *  the socket with tiny packets. Scaled to the rate so the duration stays the same. */
  sendChunkSamples: number;
}

/** The realtime models run at 24 kHz. Everything else about the connection is identical, so
 *  one endpoint serves every option and a query parameter picks. Gemini Live negotiates its own
 *  rate and will need its own profile when it is enabled. */
const REALTIME: PipelineProfile = { sampleRate: 24000, sendChunkSamples: 2400 };

const PIPELINES: Record<CallPipeline, PipelineProfile> = {
  OpenAiRealtime_2_1: REALTIME,
  GeminiLive_3_1: REALTIME,
  Unknown: REALTIME,
};

// Both names are accepted rather than one per pipeline: they mean exactly the same thing to
// this client — drop what is queued for playback — and matching on either keeps the two
// backends free to name their own signal without breaking the browser.
const FLUSH_SIGNALS = ["barge-in", "flush"];
const HANG_UP_SIGNAL = "hang-up";

// Registered via a Blob URL so the worklet needs no extra build wiring. It only converts
// each 128-sample Float32 render quantum to PCM16 and posts it out — batching happens on
// the main thread.
const CAPTURE_WORKLET = `
class CaptureProcessor extends AudioWorkletProcessor {
  process(inputs) {
    const channel = inputs[0] && inputs[0][0];
    if (channel && channel.length > 0) {
      const pcm = new Int16Array(channel.length);
      for (let i = 0; i < channel.length; i++) {
        const s = Math.max(-1, Math.min(1, channel[i]));
        pcm[i] = s < 0 ? s * 0x8000 : s * 0x7fff;
      }
      this.port.postMessage(pcm.buffer, [pcm.buffer]);
    }
    return true;
  }
}
registerProcessor("capture-processor", CaptureProcessor);
`;

export type LiveCallStatus = "connecting" | "in-call" | "ended" | "error";

export interface LiveCallCallbacks {
  onStatus: (status: LiveCallStatus, detail?: string) => void;
}

export class LiveVoiceCall {
  private socket: WebSocket | null = null;
  private audioContext: AudioContext | null = null;
  private micStream: MediaStream | null = null;
  private workletNode: AudioWorkletNode | null = null;
  private sendBuffer: Int16Array = new Int16Array(0);
  private playbackSources = new Set<AudioBufferSourceNode>();
  private nextPlayTime = 0;
  private hangingUp = false;
  private closed = false;

  private readonly profile: PipelineProfile;

  constructor(
    private readonly apiBaseUrl: string,
    private readonly token: string,
    private readonly callbacks: LiveCallCallbacks,
    private readonly pipeline: CallPipeline = "OpenAiRealtime_2_1",
  ) {
    this.profile = PIPELINES[pipeline];
  }

  async start(): Promise<void> {
    this.callbacks.onStatus("connecting");

    // Chrome resamples the mic to the context's rate automatically, so opening the context at
    // the pipeline's own rate means no manual resampling in either direction.
    this.audioContext = new AudioContext({ sampleRate: this.profile.sampleRate });
    this.micStream = await navigator.mediaDevices.getUserMedia({
      audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true, autoGainControl: true },
    });

    const workletUrl = URL.createObjectURL(new Blob([CAPTURE_WORKLET], { type: "text/javascript" }));
    try {
      await this.audioContext.audioWorklet.addModule(workletUrl);
    } finally {
      URL.revokeObjectURL(workletUrl);
    }

    const wsBaseUrl = this.apiBaseUrl.replace(/^https:\/\//, "wss://").replace(/^http:\/\//, "ws://").replace(/\/+$/, "");
    // The JWT goes in the query string, not a header — browsers can't set headers on
    // WebSocket connects, and the backend's auth middleware reads access_token for /voice paths.
    const socket = new WebSocket(
      `${wsBaseUrl}/voice/live-call?pipeline=${this.pipeline}` +
        `&access_token=${encodeURIComponent(this.token)}`,
    );
    socket.binaryType = "arraybuffer";
    this.socket = socket;

    socket.onopen = () => {
      this.beginCapture();
      this.callbacks.onStatus("in-call");
    };

    socket.onmessage = (event: MessageEvent) => {
      if (typeof event.data === "string") {
        if (FLUSH_SIGNALS.includes(event.data)) this.flushPlayback();
        if (event.data === HANG_UP_SIGNAL) this.hangUpFromServer();
        return;
      }
      this.enqueuePlayback(event.data as ArrayBuffer);
    };

    socket.onerror = () => {
      if (!this.closed && !this.hangingUp) {
        this.callbacks.onStatus("error", "connection");
        void this.dispose();
      }
    };

    socket.onclose = () => {
      if (!this.closed && !this.hangingUp) {
        // Server-initiated end without a hang-up signal (call failed, connection lost).
        this.callbacks.onStatus("ended");
        void this.dispose();
      }
    };
  }

  /** Caller pressed the red button — close from our side; the server logs the call on disconnect. */
  async hangUp(): Promise<void> {
    if (this.closed) return;
    this.hangingUp = true;
    this.stopCapture();
    this.flushPlayback();
    this.socket?.close(1000, "Caller hung up");
    this.callbacks.onStatus("ended");
    await this.dispose();
  }

  private beginCapture(): void {
    if (!this.audioContext || !this.micStream) return;
    const source = this.audioContext.createMediaStreamSource(this.micStream);
    this.workletNode = new AudioWorkletNode(this.audioContext, "capture-processor");
    this.workletNode.port.onmessage = (event: MessageEvent<ArrayBuffer>) => this.sendPcm(new Int16Array(event.data));
    source.connect(this.workletNode);
    // The worklet posts audio out via its port; nothing should reach the speakers from here.
  }

  private sendPcm(chunk: Int16Array): void {
    if (this.socket?.readyState !== WebSocket.OPEN || this.hangingUp) return;

    const merged = new Int16Array(this.sendBuffer.length + chunk.length);
    merged.set(this.sendBuffer);
    merged.set(chunk, this.sendBuffer.length);

    let offset = 0;
    const chunkSamples = this.profile.sendChunkSamples;
    while (merged.length - offset >= chunkSamples) {
      this.socket.send(merged.slice(offset, offset + chunkSamples).buffer);
      offset += chunkSamples;
    }
    this.sendBuffer = merged.slice(offset);
  }

  private enqueuePlayback(pcmBuffer: ArrayBuffer): void {
    const context = this.audioContext;
    if (!context || pcmBuffer.byteLength < 2) return;

    const pcm = new Int16Array(pcmBuffer);
    const floats = new Float32Array(pcm.length);
    for (let i = 0; i < pcm.length; i++) {
      floats[i] = pcm[i] / (pcm[i] < 0 ? 0x8000 : 0x7fff);
    }

    const buffer = context.createBuffer(1, floats.length, this.profile.sampleRate);
    buffer.getChannelData(0).set(floats);

    const sourceNode = context.createBufferSource();
    sourceNode.buffer = buffer;
    sourceNode.connect(context.destination);
    // Chunks are scheduled back to back on the context clock so consecutive frames play
    // gaplessly no matter how bursty the network delivery is.
    const startAt = Math.max(context.currentTime, this.nextPlayTime);
    sourceNode.start(startAt);
    this.nextPlayTime = startAt + buffer.duration;
    this.playbackSources.add(sourceNode);
    sourceNode.onended = () => this.playbackSources.delete(sourceNode);
  }

  private flushPlayback(): void {
    for (const sourceNode of this.playbackSources) {
      try {
        sourceNode.stop();
      } catch {
        /* already stopped */
      }
    }
    this.playbackSources.clear();
    this.nextPlayTime = 0;
  }

  private hangUpFromServer(): void {
    // Mirror of the console client's hang-up: stop the mic immediately, but let the queued
    // goodbye finish playing before closing the socket and reporting the end.
    this.hangingUp = true;
    this.stopCapture();

    const context = this.audioContext;
    const remainingMs = context ? Math.max(0, (this.nextPlayTime - context.currentTime) * 1000) + 200 : 0;
    window.setTimeout(() => {
      this.socket?.close(1000, "Call ended");
      this.callbacks.onStatus("ended");
      void this.dispose();
    }, remainingMs);
  }

  private stopCapture(): void {
    this.workletNode?.disconnect();
    this.workletNode = null;
    this.micStream?.getTracks().forEach((track) => track.stop());
    this.micStream = null;
  }

  private async dispose(): Promise<void> {
    if (this.closed) return;
    this.closed = true;
    this.stopCapture();
    this.flushPlayback();
    if (this.audioContext && this.audioContext.state !== "closed") {
      await this.audioContext.close().catch(() => undefined);
    }
    this.audioContext = null;
    this.socket = null;
  }
}
