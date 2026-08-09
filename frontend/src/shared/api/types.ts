// Mirrors AiAppointment.Application.Dtos field-for-field. Ids are int identity values
// (plain JSON numbers) and NodaTime Instants serialize as ISO-8601 strings — kept in sync
// deliberately, not by accident.

export type AccountRole = "PlatformAdmin" | "Owner" | "Staff" | "Agent";
/** BaseEntity.Status — shared by every entity (soft-delete / active flag). */
export type EntityStatus = "Active" | "Inactive";
export type AppointmentStatus = "Pending" | "Confirmed" | "Cancelled";
export type AppointmentCreatedBy = "Agent" | "Staff";
export type CallClassification =
  | "NewAppointment"
  | "UpdateReschedule"
  | "Cancellation"
  | "ReminderConfirmation"
  | "InquiryOther";
export type CallOutcome =
  | "ResolvedByAgent"
  | "EscalatedResolvedByStaff"
  | "EscalatedAbandoned"
  | "FailedNoAvailability"
  | "FailedAgentLimitation"
  | "NoAnswer";
export type EscalationStatus = "Ringing" | "Connected" | "Abandoned";

/** Which voice pipeline served a call. Named rather than numbered, so a model version is
 *  visible in the Call Log without a lookup. Mirrors Domain/Common/Enums/CallEnums.cs. */
export type CallPipeline =
  | "Unknown"
  | "OpenAiRealtime_2_1"
  | "GeminiLive_3_1";

/** Everything the Dashboard breaks spend down by, including options not yet dialable — a
 *  missing row reads as "no data", when the useful fact is "not built yet". Whether one can
 *  actually be dialled is PIPELINE_INFO[x].enabled. */
export const CALL_PIPELINES: CallPipeline[] = [
  "OpenAiRealtime_2_1",
  "GeminiLive_3_1",
];

// ---- Auth ----
export interface LoginRequest {
  email: string;
  password: string;
}
export interface LoginResponse {
  token: string;
  accountId: number;
  role: AccountRole;
  tenantId: number | null;
}
/** What a tenant can be granted. Hardcoded on both sides — a module is code, not data. Mirrors
 *  Domain/Common/Enums/Module.cs; the names are the wire values. */
export type Module = "Appointment" | "Information" | "Reminder" | "Feedback" | "Order" | "Survey";

/** Display order for the admin screen and the picker. */
export const MODULES: Module[] = ["Appointment", "Information", "Reminder", "Feedback", "Order", "Survey"];

export interface MeResponse {
  id: number;
  name: string;
  email: string;
  role: AccountRole;
  tenantId: number | null;
  tenantName: string | null;
  /** What this tenant holds. Empty for a platform admin, who administers grants rather than
   *  holding any. One module routes straight through; several show the picker. */
  enabledModules: Module[];
}

export interface TenantModuleResponse {
  module: Module;
  enabled: boolean;
}

// ---- Tenant ----
export interface TenantResponse {
  id: number;
  name: string;
  timezone: string;
  phoneLine: string | null;
  status: EntityStatus;
  createdAt: string;
}
export interface CreateTenantRequest {
  name: string;
  timezone: string;
  phoneLine: string | null;
  ownerName: string;
  ownerEmail: string;
  ownerPassword: string;
}
export interface CreateTenantResult {
  tenant: TenantResponse;
  ownerAccountId: number;
  agentAccountId: number;
  agentApiKey: string;
}
export interface UpdateTenantRequest {
  name: string;
  timezone: string;
  phoneLine: string | null;
}

// ---- Account (staff) ----
export interface AccountResponse {
  id: number;
  name: string;
  email: string;
  role: AccountRole;
  status: EntityStatus;
}
export interface AddStaffRequest {
  name: string;
  email: string;
  password: string;
}
export interface UpdateAccountRequest {
  name: string;
}
export interface ResetAccountPasswordRequest {
  password: string;
}

// ---- Provider ----
export interface ProviderResponse {
  id: number;
  name: string;
}
export interface CreateProviderRequest {
  name: string;
}
export interface UpdateProviderRequest {
  name: string;
}
export interface ProviderServiceAssignmentResponse {
  providerId: number;
  serviceOfferingId: number;
  active: boolean;
}
export interface ProviderServiceMatrixResponse {
  providers: ProviderResponse[];
  serviceOfferings: ServiceOfferingResponse[];
  assignments: ProviderServiceAssignmentResponse[];
}
export interface SetProviderServiceAssignmentRequest {
  serviceOfferingId: number;
  active: boolean;
}

// ---- Service offering ----
export interface ServiceOfferingResponse {
  id: number;
  name: string;
  price: number;
  durationMinutes: number;
  updatedAt: string;
}
export interface CreateServiceOfferingRequest {
  name: string;
  price: number;
  durationMinutes: number;
}
export interface UpdateServiceOfferingRequest {
  name: string;
  price: number;
  durationMinutes: number;
}

// ---- Client ----
export interface ClientResponse {
  id: number;
  phoneNumber: string;
  name: string | null;
  blackListed: boolean;
  blackListReason: string | null;
  createdAt: string;
}
export interface CreateClientRequest {
  phoneNumber: string;
  name: string | null;
}
export interface UpdateClientRequest {
  phoneNumber: string;
  name: string | null;
}
export interface BlackListClientRequest {
  reason: string | null;
}

// ---- Appointment ----
export interface AppointmentResponse {
  id: number;
  clientId: number;
  clientPhoneNumber: string;
  clientName: string | null;
  providerId: number;
  serviceOfferingId: number;
  start: string;
  end: string;
  status: AppointmentStatus;
  reminderNoAnswerFlag: boolean;
  notes: string | null;
  createdBy: AppointmentCreatedBy;
}
export interface CreateAppointmentRequest {
  clientPhoneNumber: string;
  clientName: string | null;
  providerId: number;
  serviceOfferingId: number;
  start: string;
  end: string;
  notes: string | null;
}
export interface RescheduleAppointmentRequest {
  providerId: number;
  serviceOfferingId: number;
  start: string;
  end: string;
}
export interface AvailableSlot {
  start: string;
  end: string;
}
export interface AvailabilityResponse {
  slots: AvailableSlot[];
}

// ---- Call ----
/** The six token kinds priced separately by the model provider. Cached counts are already
 *  carved out of the uncached ones, so these six sum to the call's total. */
export interface TokenUsage {
  inputTextTokens: number;
  cachedInputTextTokens: number;
  inputAudioTokens: number;
  cachedInputAudioTokens: number;
  outputTextTokens: number;
  outputAudioTokens: number;
  totalTokens: number;
}
export interface CallResponse {
  id: number;
  clientId: number | null;
  callerPhoneNumber: string;
  clientName: string | null;
  relatedAppointmentId: number | null;
  classification: CallClassification;
  outcome: CallOutcome;
  durationSeconds: number;
  /** Answers the agent completed. */
  turnCount: number;
  /** Questions the caller asked. */
  callerTurnCount: number;
  waitTimeSeconds: number | null;
  recordingUrl: string;
  startedAt: string;
  agentModel: string;
  pipeline: CallPipeline;
  tokenUsage: TokenUsage;
  /** Priced when the call was logged, at the rates in force then — never recalculated. */
  costUsd: number;
  /** Null when the call was too short (or had too few answers) for a rate to mean anything. */
  costPerMinuteUsd: number | null;
  costPerAnswerUsd: number | null;
}
export interface CallDetailResponse {
  call: CallResponse;
  transcript: string | null;
}

// ---- Escalation ----
export interface EscalationResponse {
  id: number;
  clientId: number;
  callerPhoneNumber: string;
  clientName: string | null;
  reason: string;
  status: EscalationStatus;
  raisedAt: string;
  acceptedByAccountId: number | null;
  acceptedAt: string | null;
}

// ---- Dashboard ----
export interface DashboardSummaryResponse {
  totalCalls: number;
  resolvedByAgentRate: number;
  resolvedByAgentCount: number;
  escalationRate: number;
  escalationCount: number;
  escalationResolvedByStaffRate: number;
  escalationAbandonedRate: number;
  failedNoAvailabilityRate: number;
  failedAgentLimitationRate: number;
  averageCallDurationSeconds: number;
  averageTurnsToResolution: number;
  appointmentVolume: number;
  reminderNoAnswerRate: number;
  totalCostUsd: number;
  averageCostPerCallUsd: number;
  /** Weighted by call length, not an average of each call's own rate. */
  averageCostPerMinuteUsd: number;
  averageCostPerAnswerUsd: number;
  totalTokens: number;
  /** One row per pipeline that actually served a call in the range. Pipelines nobody dialled
   *  are omitted — a "$0.00 over 0 calls" row reads as free rather than unused. */
  spendByPipeline: PipelineSpend[];
}
export interface PipelineSpend {
  pipeline: CallPipeline;
  calls: number;
  totalCostUsd: number;
  averageCostPerCallUsd: number;
  averageCostPerMinuteUsd: number;
  averageCallDurationSeconds: number;
  /** The models actually billed — the pipeline name gives the architecture, this the receipt. */
  models: string;
}

// ---- Orders module ----

export interface UnitResponse {
  id: number;
  name: string;
}

export interface ProductDetailResponse {
  id: number;
  name: string;
  description: string | null;
  measurementUnitId: number;
  unit: string;
  unitPrice: number;
  /** Comma-separated words a caller might use — "bidon, balon, su". The field that decides
   *  whether the agent recognises what someone is asking for. */
  aliases: string | null;
}

export interface CreateProductRequest {
  name: string;
  description: string | null;
  measurementUnitId: number;
  unitPrice: number;
  aliases: string | null;
}

export type UpdateProductRequest = CreateProductRequest;

export interface OrderLineResponse {
  productName: string;
  quantity: number;
  unit: string;
  lineTotal: number;
}

export interface OrderResponse {
  id: number;
  customerId: number;
  customerName: string | null;
  deliveryAddress: string;
  status: string;
  placedAt: string;
  requestedDeliveryDate: string | null;
  notes: string | null;
  lines: OrderLineResponse[];
  total: number;
}

export interface OrderCustomerResponse {
  id: number;
  name: string | null;
  phoneNumbers: string[];
  addresses: string[];
  createdAt: string;
}

export interface OrderSettingsResponse {
  leadWorkingDays: number;
}

/** Both times null means the business is shut that day — one fact, not a separate flag that
 *  could disagree with the times beside it. */
export interface BusinessHoursDay {
  dayOfWeek: string;
  opensAt: string | null;
  closesAt: string | null;
}