import type { CallPipeline } from "@/shared/api/types";
import type { Language } from "@/shared/i18n/translations";

// The legend. One table so the Call page picker, the Call Log column and the Dashboard
// breakdown all describe a pipeline the same way — and so the trade-off each one represents is
// visible at the moment of choosing, not buried in a commit message.

export interface PipelineInfo {
  /** Short label for a table cell or a toggle. */
  short: string;
  /** What it actually is, architecturally. Keyed by Language, so a new one is a build error. */
  what: Record<Language, string>;
  /** The trade-off, in one line. This is the part that makes the legend worth having. */
  tradeOff: Record<Language, string>;
  /** Roughly what a minute costs, from measured calls. */
  costPerMinute: string;
  /** False for an option that is listed but cannot be dialled yet. Shown greyed out rather
   *  than hidden, so the picker does not change shape the day it ships. Must match
   *  VoicePipelineCatalog on the server — that is what actually refuses the call. */
  enabled: boolean;
}

export const PIPELINE_INFO: Record<CallPipeline, PipelineInfo> = {
  OpenAiRealtime_2_1: {
    short: "OpenAI · realtime 2.1",
    what: {
      az: "Bir realtime model səsi birbaşa emal edir (gpt-realtime).",
      ru: "Одна realtime-модель обрабатывает звук напрямую (gpt-realtime).",
      en: "A single realtime model handles the audio directly (gpt-realtime).",
    },
    tradeOff: {
      az: "Ən sürətli və Azərbaycan dilini ən yaxşı başa düşən — ən bahalısı.",
      ru: "Самый быстрый и лучше всех понимает азербайджанский — самый дорогой.",
      en: "Fastest, and the best at understanding Azerbaijani — also the most expensive.",
    },
    costPerMinute: "~$0.10",
    enabled: true,
  },
  GeminiLive_3_1: {
    short: "Gemini · Live 3.1",
    what: {
      az: "Gemini Live səsi birbaşa emal edir — ayrıca protokol.",
      ru: "Gemini Live обрабатывает звук напрямую — отдельный протокол.",
      en: "Gemini Live handles the audio directly — a separate wire protocol.",
    },
    tradeOff: {
      az: "Təxminən dörd dəfə ucuz. Azərbaycan dili yoxlanılmayıb — hələ aktiv deyil.",
      ru: "Примерно вчетверо дешевле. Азербайджанский не проверен — пока не включён.",
      en: "Roughly four times cheaper. Azerbaijani unverified — not enabled yet.",
    },
    costPerMinute: "~$0.025",
    enabled: false,
  },
  Unknown: {
    short: "—",
    what: {
      az: "Bu zəng növ uçotu əlavə edilməzdən əvvəl qeydə alınıb.",
      ru: "Этот звонок записан до того, как был добавлён учёт типа.",
      en: "This call was logged before mode tracking was added.",
    },
    tradeOff: { az: "", ru: "", en: "" },
    costPerMinute: "—",
    enabled: false,
  },
};

export function pipelineLabel(pipeline: CallPipeline): string {
  return PIPELINE_INFO[pipeline]?.short ?? pipeline;
}
