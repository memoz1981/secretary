import { useRef, useState } from "react";
import { useLanguage } from "@/shared/i18n/LanguageContext";

function formatTime(seconds: number): string {
  const m = Math.floor(seconds / 60);
  const s = Math.floor(seconds % 60);
  return `${m}:${String(s).padStart(2, "0")}`;
}

export function AudioPlayer({ src }: { src: string }) {
  const { t } = useLanguage();
  const audioRef = useRef<HTMLAudioElement>(null);
  const [playing, setPlaying] = useState(false);
  const [current, setCurrent] = useState(0);
  const [duration, setDuration] = useState(0);

  function toggle() {
    const audio = audioRef.current;
    if (!audio) return;
    if (playing) {
      audio.pause();
    } else {
      audio.play();
    }
    setPlaying(!playing);
  }

  const pct = duration > 0 ? (current / duration) * 100 : 0;

  return (
    <div className="player">
      <audio
        ref={audioRef}
        src={src}
        onTimeUpdate={(e) => setCurrent(e.currentTarget.currentTime)}
        onLoadedMetadata={(e) => setDuration(e.currentTarget.duration)}
        onEnded={() => setPlaying(false)}
      />
      <button
        className="play-btn"
        onClick={toggle}
        aria-label={playing ? t("audioPause") : t("audioPlay")}
      >
        {playing ? "❚❚" : "▶"}
      </button>
      <div className="scrubber">
        <div className="fill" style={{ width: `${pct}%` }} />
      </div>
      <div className="time">
        {formatTime(current)} / {formatTime(duration || 0)}
      </div>
    </div>
  );
}
