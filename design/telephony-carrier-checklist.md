# Telephony — carrier and Twilio checklist

What to confirm before production telephony. Nothing here blocks development: the browser gateway
uses the same seam, so the whole pipeline can be built and both AI providers compared before any of
this is answered.

**Target setup:** +994 number stays with the local Azerbaijani carrier → carrier trunks to Twilio
(BYOC, no porting) → Twilio Media Streams websocket → our app → OpenAI or Gemini.

**Second channel:** WhatsApp Business Calling over the same Twilio path — no DID and no carrier
involved at all. See section 3; it may reach production first.

---

## 1. Ask the local Azerbaijani carrier

The first two are make-or-break. If either is "no", BYOC is off and we need a different carrier.

| | question |
|---|---|
| 1 | **Can you deliver inbound calls for our +994 number to an external SIP URI over the public internet?** Some local carriers only hand calls to in-country equipment. |
| 2 | **Are there regulatory restrictions on routing Azerbaijani numbers to foreign infrastructure?** The same question from the legal side. |
| 3 | Which SIP transport — **UDP, TCP or TLS**? Twilio accepts all; TLS preferred. |
| 4 | Do you support **outbound digest authentication** (username/password), or IP-based only? Twilio warns that IP ACLs alone don't protect against some attacks and recommends credentials. |
| 5 | What are your **SBC source IP addresses**? Twilio needs these for the ACL. |
| 6 | Which **codecs**? We need G.711 — μ-law (PCMU) or A-law (PCMA). The region typically defaults to A-law; either is fine, just confirm. |
| 7 | Do you require **SRTP / encrypted media**? |
| 8 | Will you **accept outbound calls** from Twilio too? Not needed yet, but the Reminders, Feedback and Survey modules are all outbound. |

## 2. Ask Twilio

| | question |
|---|---|
| 1 | **Does Media Streams work on BYOC-originated calls?** ⚠️ **This is the crux** — the entire architecture assumes it. Media Streams is a Programmable Voice feature and BYOC adds Programmable Voice to the call, so it should, but get it confirmed explicitly. |
| 2 | Is **BYOC Trunking available for a carrier in Azerbaijan**? No country restrictions are documented, but none are confirmed either. |
| 3 | Which **termination edge** should an Azerbaijani carrier target? Termination URIs are regional. |
| 4 | **BYOC per-minute pricing** — a separate rate from standard Programmable Voice. Needed for the real cost model. |

## 3. Ask Twilio — WhatsApp Business Calling

Raise these in the **same conversation** as section 2. The answers could reorder the path to
production: WhatsApp needs no +994 DID and no carrier interconnect, so if it is available and the
carrier is slow, WhatsApp ships first.

| | question |
|---|---|
| 1 | **Does Media Streams work on WhatsApp Business Calling?** ⚠️ Same crux as BYOC — the whole channel depends on it. |
| 2 | **What audio format arrives via Media Streams — Opus wideband passed through, or transcoded down to μ-law 8 kHz?** WhatsApp bypasses the PSTN, and narrowband measurably degrades speech recognition. Azerbaijani recognition is this project's core constraint, so wideband is a real quality gain — but only if Twilio doesn't flatten it. |
| 3 | **Is WhatsApp Business Calling available for Azerbaijan** — inbound and outbound separately? Business-initiated calling is restricted in some markets. |
| 4 | What does the **outbound permission flow** require? Outbound calls need explicit user consent, which affects the Reminders, Feedback and Survey modules. |
| 5 | **Per-minute pricing** for WhatsApp calls versus PSTN. |

Also needed, and not a Twilio question: a **WhatsApp Business Account (WABA)** and Meta business
verification. Process friction, worth starting early.

## 4. Reference facts

**Twilio termination SIP domain** (what the carrier points at):
```
{name}.sip.twilio.com            global
{name}.sip.ie1.twilio.com        regional variants
```

**Authentication:** minimum of an IP ACL *or* credentials; if both are configured, both are enforced.

**Twilio public edges:** dublin, frankfurt, ashburn, umatilla, sydney, sao-paulo, tokyo, singapore.
**No Middle East or Caucasus edge exists** — Frankfurt is closest to Baku, roughly 60–80 ms.

**Twilio media:** `168.86.128.0/18`, UDP ports 10000–60000. Needed for firewall rules.

**Cost:** Media Streams $0.004/min on top of Programmable Voice minutes and number rental.

---

## 5. Do this first

**Decouple "does our code work" from "will the carrier cooperate."**

Buy a throwaway Twilio number in a fully supported country — UK or US, ~$1/month — and build the
Media Streams path against it. That proves webhook, TwiML, websocket, μ-law transcoding, OpenAI,
Gemini, barge-in and hangup end to end, without a single conversation with the Azerbaijani carrier.

Then BYOC with the real +994 number introduces exactly **one** new variable. If it fails, it is
unambiguously the interconnect and not our code.

This also means every question above can be asked in parallel with development rather than ahead
of it.
