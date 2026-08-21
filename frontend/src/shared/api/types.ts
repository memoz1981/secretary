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
  /** What this tenant can actually use. */
  enabledModules: Module[];
  id: number;
  name: string;
  timezone: string;
  phoneLine: string | null;
  /** Whether this tenant may be shown what their AI calls cost. The platform's switch, off by
   *  default — the margin between what a call costs and what they pay is readable from it. */
  showCallCosts: boolean;
  status: EntityStatus;
  createdAt: string;
}
export interface CreateTenantRequest {
  name: string;
  timezone: string;
  phoneLine: string | null;
  showCallCosts: boolean;
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
  showCallCosts: boolean;
}

/** What a tenant may change about themselves — the same details minus the one that is not
 *  theirs. A request type is the list of things a caller is allowed to say, and leaving a field
 *  on it is permission. */
export interface UpdateOwnTenantRequest {
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
  /** Null when this caller may not be shown call costs — see the tenant's showCallCosts. */
  costUsd: number | null;
  /** Null when the call was too short (or had too few answers) for a rate to mean anything. */
  costPerMinuteUsd: number | null;
  costPerAnswerUsd: number | null;
}
/** One row of the order line's call log. Narrower than CallResponse on purpose: no
 * classification, and `resolvedByAgent` collapses the six outcomes into the one question the
 * page is actually asking. */
export interface OrderCallResponse {
  id: number;
  customerId: number | null;
  customerName: string | null;
  callerPhoneNumber: string;
  /** The order this call produced, when it produced one. */
  relatedOrderId: number | null;
  outcome: CallOutcome;
  durationSeconds: number;
  /** Answers the agent completed. */
  turnCount: number;
  /** Questions the caller asked. */
  callerTurnCount: number;
  startedAt: string;
  agentModel: string;
  pipeline: CallPipeline;
  tokenUsage: TokenUsage;
  /** Null when this caller may not be shown call costs — see the tenant's showCallCosts. */
  costUsd: number | null;
  costPerMinuteUsd: number | null;
  costPerAnswerUsd: number | null;
  resolvedByAgent: boolean;
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
  totalCostUsd: number | null;
  averageCostPerCallUsd: number | null;
  /** Weighted by call length, not an average of each call's own rate. */
  averageCostPerMinuteUsd: number | null;
  averageCostPerAnswerUsd: number | null;
  totalTokens: number;
  /** One row per pipeline that actually served a call in the range. Pipelines nobody dialled
   *  are omitted — a "$0.00 over 0 calls" row reads as free rather than unused. */
  spendByPipeline: PipelineSpend[];
}
export interface PipelineSpend {
  pipeline: CallPipeline;
  calls: number;
  totalCostUsd: number | null;
  averageCostPerCallUsd: number | null;
  averageCostPerMinuteUsd: number | null;
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
  measurementUnitId: number;
  unit: string;
  unitPrice: number;
  /** The most of this that one order may contain. Null means no limit. */
  maxOrderQuantity: number | null;
  /** Comma-separated words a caller might use — "bidon, balon, su". The field that decides
   *  whether the agent recognises what someone is asking for. */
  aliases: string | null;
}

export interface CreateProductRequest {
  name: string;
  measurementUnitId: number;
  unitPrice: number;
  aliases: string | null;
  maxOrderQuantity: number | null;
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
  /** How far ahead an order may be placed, in calendar days. */
  maxDeliveryDaysAhead: number;
}

/** Both times null means the business is shut that day — one fact, not a separate flag that
 *  could disagree with the times beside it. */
export interface BusinessHoursDay {
  dayOfWeek: string;
  opensAt: string | null;
  closesAt: string | null;
}

// ---- Feedback module ----

export type FeedbackQuestionType = "Open" | "Choice" | "YesNo" | "Scale";
export type FeedbackCallStatus = "Created" | "InProgress" | "Completed" | "Abandoned" | "NeedsHuman";

/** A scale runs to one of these, and no other number. "1 to 7" is a scale nobody can hold in
 *  their head while listening. */
export const SCALE_MAXIMUMS = [3, 5, 10] as const;

export interface SurveyOptionResponse {
  id: number;
  position: number;
  text: string;
  /** 0-100, derived by the question. Null for a Choice option, where there is no ordering to
   *  average and so nothing honest to score. Never typed by anybody. */
  scorePercent: number | null;
  /** The "Digər" option, which also records what the caller actually said. */
  isOther: boolean;
}

export interface SurveyQuestionResponse {
  id: number;
  position: number;
  text: string;
  questionType: FeedbackQuestionType;
  /** Whether this question's score feeds the questionnaire's one number. Any number of questions
   *  may; it replaced a single "headline" tick. */
  countsTowardScore: boolean;
  /** Scale only. */
  scaleMax: number | null;
  /** Yes/No only. False when "Xeyr" is the good answer. */
  yesIsPositive: boolean;
  /** Choice only. */
  allowOther: boolean;
  options: SurveyOptionResponse[];
  /** Yes/No or Scale — the two types with an ordering, and so the two that can be averaged. */
  isScored: boolean;
}

export interface SurveyResponse {
  id: number;
  name: string;
  questionCount: number;
  /** How many more times to ring somebody who never answered. Zero means one attempt. */
  retryCount: number;
  retryDelayMinutes: number;
}

export interface SaveRetryPolicyRequest {
  retryCount: number;
  retryDelayMinutes: number;
}

export interface SurveyDetailResponse {
  id: number;
  name: string;
  questions: SurveyQuestionResponse[];
}

export interface SaveSurveyRequest {
  name: string;
}

/** What the question editor sends. Nothing numeric except the size of a scale — Yes/No and Scale
 *  build their own options and their own percentages, and Choice has no scores at all.
 *
 *  Fields that do not apply to the chosen type are ignored by the server rather than rejected, so
 *  switching type in the form does not have to clear them first. */
export interface SaveQuestionRequest {
  text: string;
  questionType: FeedbackQuestionType;
  countsTowardScore: boolean;
  scaleMax: number | null;
  yesIsPositive: boolean;
  allowOther: boolean;
  labels: string[];
}

export interface FeedbackSettingsResponse {
  maxSurveys: number;
  usedSurveys: number;
}

export interface QueueFeedbackCallRequest {
  surveyId: number;
  personName: string;
  phoneNumber: string;
}

/** What became of one person we set out to survey — across every attempt, not one dial.
 *
 *  There is deliberately no "partial": a survey that stopped half way is not half a result, its
 *  answers are not reported, and the person is not counted as surveyed. */
export type SurveyRequestOutcome = "Pending" | "NotReached" | "Refused" | "NeedsHuman" | "Complete";

export interface SurveyRequestResponse {
  id: number;
  surveyId: number;
  surveyName: string;
  personName: string;
  phoneNumber: string;
  outcome: SurveyRequestOutcome;
  attemptCount: number;
  createdAt: string;
  lastAttemptAt: string | null;
  /** When the next dial is due, or null when there will not be one. */
  nextAttemptDueAt: string | null;
  /** Somebody a person still has to deal with — a breakdown, or a number that never answered
   *  and has run out of retries. A refusal needs nobody. */
  needsFollowUp: boolean;
  totalCostUsd: number | null;
}

export interface FeedbackCallResponse {
  id: number;
  surveyRequestId: number;
  surveyId: number;
  surveyName: string;
  personName: string;
  phoneNumber: string;
  /** Which dial this was against the person — 1 for the first. */
  attemptNumber: number;
  status: FeedbackCallStatus;
  createdAt: string;
  completedAt: string | null;
  durationSeconds: number;
  turnCount: number;
  callerTurnCount: number;
  agentModel: string;
  pipeline: CallPipeline;
  tokenUsage: TokenUsage;
  /** Null when this caller may not be shown call costs — see the tenant's showCallCosts. */
  costUsd: number | null;
  answeredCount: number;
  questionCount: number;
  isCompleted: boolean;
  costPerMinuteUsd: number | null;
}

export interface FeedbackAnswerResponse {
  questionId: number;
  position: number;
  questionText: string;
  questionType: FeedbackQuestionType;
  optionText: string | null;
  optionScorePercent: number | null;
  text: string | null;
  /** Asked, and would not say. Distinct from never having been asked. */
  declined: boolean;
  answeredAt: string;
}

export interface FeedbackCallDetailResponse {
  call: FeedbackCallResponse;
  answers: FeedbackAnswerResponse[];
  transcript: string | null;
}

/** Counted in people, never in dials. Counting rows in the calls table meant four dead
 *  connections in nine seconds read as four surveys attempted. */
export interface FeedbackCoverage {
  requested: number;
  completed: number;
  notReached: number;
  refused: number;
  needsHuman: number;
  attempts: number;
  averageDurationSeconds: number;
  totalCostUsd: number | null;
  /** People who picked up and engaged. */
  reached: number;
  /** Completed ÷ reached. Never ÷ requested — a wrong phone number is not the agent failing at
   *  a conversation. */
  completionRate: number | null;
  /** Completed ÷ requested. What qualifies the score. */
  responseRate: number | null;
}

export interface OptionBreakdown {
  optionId: number;
  text: string;
  scorePercent: number | null;
  count: number;
}

export interface QuestionResult {
  questionId: number;
  position: number;
  questionText: string;
  questionType: FeedbackQuestionType;
  countsTowardScore: boolean;
  isScored: boolean;
  answeredCount: number;
  declinedCount: number;
  /** A mean of the option percentages. Null for Choice and Open, which have no ordering. */
  averagePercent: number | null;
  options: OptionBreakdown[];
}

export interface FeedbackDashboardResponse {
  surveyId: number;
  surveyName: string;
  /** The mean of every answer to a question ticked "counts toward the score". */
  scorePercent: number | null;
  /** The same number over the preceding window of equal length. Null for an all-time view. */
  previousScorePercent: number | null;
  scoreAnswerCount: number;
  coverage: FeedbackCoverage;
  /** Everything except Open questions — free text has nothing honest to chart and is read on
   *  the call detail page instead. */
  results: QuestionResult[];
}
