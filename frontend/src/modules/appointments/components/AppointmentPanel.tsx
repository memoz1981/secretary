import { useState, type FormEvent } from "react";
import { SidePanel } from "@/shared/components/SidePanel";
import { Button } from "@/shared/components/Button";
import { Pill } from "@/shared/components/Pill";
import { TextField, SelectField, TextAreaField } from "@/shared/components/FormControls";
import { useAuth } from "@/shared/auth/AuthContext";
import { cancelAppointment, createAppointment, rescheduleAppointment } from "@/modules/appointments/api/appointments";
import type { AppointmentResponse, ProviderResponse, ServiceOfferingResponse } from "@/shared/api/types";
import { isRequired } from "@/shared/lib/validation";
import { useLanguage } from "@/shared/i18n/LanguageContext";
import { appointmentStatusLabels, translateEnum } from "@/shared/i18n/translations";

interface AppointmentPanelProps {
  providers: ProviderResponse[];
  serviceOfferings: ServiceOfferingResponse[];
  existing?: AppointmentResponse;
  defaultStart?: string;
  onClose: () => void;
  onSaved: () => void;
}

function toLocalInputValue(iso: string): string {
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export function AppointmentPanel({ providers, serviceOfferings, existing, defaultStart, onClose, onSaved }: AppointmentPanelProps) {
  const { token } = useAuth();
  const { language, t } = useLanguage();
  const [clientPhoneNumber, setClientPhoneNumber] = useState(existing?.clientPhoneNumber ?? "");
  const [clientName, setClientName] = useState(existing?.clientName ?? "");
  // Select element values are strings; ids convert back to numbers on submit.
  const [providerId, setProviderId] = useState(existing ? String(existing.providerId) : providers[0] ? String(providers[0].id) : "");
  const [serviceOfferingId, setServiceOfferingId] = useState(
    existing ? String(existing.serviceOfferingId) : serviceOfferings[0] ? String(serviceOfferings[0].id) : "",
  );
  const [startLocal, setStartLocal] = useState(existing ? toLocalInputValue(existing.start) : defaultStart ? toLocalInputValue(defaultStart) : "");
  const [notes, setNotes] = useState(existing?.notes ?? "");
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitting, setSubmitting] = useState(false);
  const [confirmingCancel, setConfirmingCancel] = useState(false);

  const selectedService = serviceOfferings.find((s) => String(s.id) === serviceOfferingId);

  async function handleSave(e: FormEvent) {
    e.preventDefault();
    const nextErrors: Record<string, string> = {};
    if (!isRequired(clientPhoneNumber)) nextErrors.clientPhoneNumber = t("clientPhoneRequired");
    if (!providerId) nextErrors.providerId = t("chooseAProvider");
    if (!serviceOfferingId) nextErrors.serviceOfferingId = t("chooseAService");
    if (!startLocal) nextErrors.start = t("chooseDateAndTime");
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) return;

    const start = new Date(startLocal);
    const durationMinutes = selectedService?.durationMinutes ?? 30;
    const end = new Date(start.getTime() + durationMinutes * 60_000);

    setSubmitting(true);
    try {
      if (existing) {
        await rescheduleAppointment(token!, existing.id, {
          providerId: Number(providerId),
          serviceOfferingId: Number(serviceOfferingId),
          start: start.toISOString(),
          end: end.toISOString(),
        });
      } else {
        await createAppointment(token!, {
          clientPhoneNumber,
          clientName: clientName || null,
          providerId: Number(providerId),
          serviceOfferingId: Number(serviceOfferingId),
          start: start.toISOString(),
          end: end.toISOString(),
          notes: notes || null,
        });
      }
      onSaved();
    } finally {
      setSubmitting(false);
    }
  }

  async function handleCancelAppointment() {
    if (!existing) return;
    if (!confirmingCancel) {
      setConfirmingCancel(true);
      return;
    }
    await cancelAppointment(token!, existing.id);
    onSaved();
  }

  return (
    <SidePanel
      title={
        <>
          {existing ? t("editAppointment") : t("newAppointment")}
          {existing && (
            <Pill variant={existing.status === "Confirmed" ? "success" : "neutral"}>
              {translateEnum(appointmentStatusLabels, existing.status, language)}
            </Pill>
          )}
        </>
      }
      onClose={onClose}
    >
      <form onSubmit={handleSave}>
        <TextField
          label={t("clientPhone")}
          value={clientPhoneNumber}
          onChange={(e) => setClientPhoneNumber(e.target.value)}
          error={errors.clientPhoneNumber}
          disabled={!!existing}
          placeholder="+994 XX XXX XX XX"
        />
        <TextField label={t("clientName")} value={clientName} onChange={(e) => setClientName(e.target.value)} disabled={!!existing} />
        <SelectField
          label={t("service")}
          value={serviceOfferingId}
          onChange={(e) => setServiceOfferingId(e.target.value)}
          error={errors.serviceOfferingId}
          options={serviceOfferings.map((s) => ({ value: String(s.id), label: `${s.name} — ${s.price} AZN, ${s.durationMinutes} min` }))}
        />
        <SelectField
          label={t("provider")}
          value={providerId}
          onChange={(e) => setProviderId(e.target.value)}
          error={errors.providerId}
          options={providers.map((p) => ({ value: String(p.id), label: p.name }))}
        />
        <TextField
          label={t("dateAndTime")}
          type="datetime-local"
          value={startLocal}
          onChange={(e) => setStartLocal(e.target.value)}
          error={errors.start}
        />
        {!existing && <TextAreaField label={t("notes")} placeholder={t("optional")} value={notes} onChange={(e) => setNotes(e.target.value)} />}
        <div className="actions">
          <Button type="submit" loading={submitting}>
            {t("save")}
          </Button>
          {existing && (
            <Button type="button" variant="danger" onClick={handleCancelAppointment}>
              {confirmingCancel ? t("confirmCancel") : t("cancelAppointment")}
            </Button>
          )}
        </div>
        <div className="note">{t("cancelConfirmationNote")}</div>
      </form>
    </SidePanel>
  );
}
