import { apiFetch, toQueryString } from "@/shared/api/client";
import type {
  FeedbackCallDetailResponse,
  FeedbackCallResponse,
  FeedbackDashboardResponse,
  FeedbackSettingsResponse,
  QueueFeedbackCallRequest,
  SaveQuestionRequest,
  SaveSurveyRequest,
  SurveyDetailResponse,
  SurveyQuestionResponse,
  SurveyResponse,
} from "@/shared/api/types";

// ---- Questionnaires ----

export function getSurveys(token: string) {
  return apiFetch<SurveyResponse[]>("/api/feedback/surveys", { token });
}

export function getSurveyQuota(token: string) {
  return apiFetch<FeedbackSettingsResponse>("/api/feedback/surveys/quota", { token });
}

export function getSurvey(token: string, id: number) {
  return apiFetch<SurveyDetailResponse>(`/api/feedback/surveys/${id}`, { token });
}

export function createSurvey(token: string, request: SaveSurveyRequest) {
  return apiFetch<SurveyResponse>("/api/feedback/surveys", { method: "POST", body: request, token });
}

export function renameSurvey(token: string, id: number, request: SaveSurveyRequest) {
  return apiFetch<SurveyResponse>(`/api/feedback/surveys/${id}`, { method: "PUT", body: request, token });
}

export function removeSurvey(token: string, id: number) {
  return apiFetch<void>(`/api/feedback/surveys/${id}`, { method: "DELETE", token });
}

export function addQuestion(token: string, surveyId: number, request: SaveQuestionRequest) {
  return apiFetch<SurveyQuestionResponse>(`/api/feedback/surveys/${surveyId}/questions`, {
    method: "POST",
    body: request,
    token,
  });
}

export function updateQuestion(token: string, questionId: number, request: SaveQuestionRequest) {
  return apiFetch<SurveyQuestionResponse>(`/api/feedback/surveys/questions/${questionId}`, {
    method: "PUT",
    body: request,
    token,
  });
}

export function removeQuestion(token: string, questionId: number) {
  return apiFetch<void>(`/api/feedback/surveys/questions/${questionId}`, { method: "DELETE", token });
}

/** The whole running order at once — one request per moved question would leave the list
 *  half-reordered if any of them failed. */
export function reorderQuestions(token: string, surveyId: number, questionIdsInOrder: number[]) {
  return apiFetch<SurveyDetailResponse>(`/api/feedback/surveys/${surveyId}/questions/order`, {
    method: "PUT",
    body: { questionIdsInOrder },
    token,
  });
}

// ---- Calls ----

/** Records who is about to be rung and returns the id the call is dialled with. The one endpoint
 *  a scheduler will call instead of a person, the day telephony lands. */
export function queueFeedbackCall(token: string, request: QueueFeedbackCallRequest) {
  return apiFetch<FeedbackCallResponse>("/api/feedback/calls", { method: "POST", body: request, token });
}

export interface FeedbackCallSearchParams {
  from?: string;
  to?: string;
  surveyId?: number;
}

export function searchFeedbackCalls(token: string, params: FeedbackCallSearchParams) {
  return apiFetch<FeedbackCallResponse[]>(`/api/feedback/calls${toQueryString({ ...params })}`, { token });
}

export function getFeedbackCall(token: string, id: number) {
  return apiFetch<FeedbackCallDetailResponse>(`/api/feedback/calls/${id}`, { token });
}

export function getFeedbackDashboard(token: string, surveyId: number, params: { from?: string; to?: string }) {
  return apiFetch<FeedbackDashboardResponse>(
    `/api/feedback/calls/dashboard/${surveyId}${toQueryString({ ...params })}`,
    { token },
  );
}
