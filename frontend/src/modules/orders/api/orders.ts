import { apiFetch } from "@/shared/api/client";
import type {
  CreateProductRequest,
  OrderCustomerResponse,
  OrderResponse,
  OrderSettingsResponse,
  ProductDetailResponse,
  UnitResponse,
  UpdateProductRequest,
} from "@/shared/api/types";

export function getProducts(token: string) {
  return apiFetch<ProductDetailResponse[]>("/api/products", { token });
}

export function getUnits(token: string) {
  return apiFetch<UnitResponse[]>("/api/products/units", { token });
}

export function createProduct(token: string, request: CreateProductRequest) {
  return apiFetch<ProductDetailResponse>("/api/products", { method: "POST", body: request, token });
}

export function updateProduct(token: string, id: number, request: UpdateProductRequest) {
  return apiFetch<ProductDetailResponse>(`/api/products/${id}`, { method: "PUT", body: request, token });
}

export function removeProduct(token: string, id: number) {
  return apiFetch<void>(`/api/products/${id}`, { method: "DELETE", token });
}

export function getOrders(token: string) {
  return apiFetch<OrderResponse[]>("/api/orders", { token });
}

export function getOrderCustomers(token: string) {
  return apiFetch<OrderCustomerResponse[]>("/api/orders/customers", { token });
}

export function getOrderSettings(token: string) {
  return apiFetch<OrderSettingsResponse>("/api/orders/settings", { token });
}

export function updateOrderSettings(token: string, leadWorkingDays: number) {
  return apiFetch<OrderSettingsResponse>("/api/orders/settings", {
    method: "PUT",
    body: { leadWorkingDays },
    token,
  });
}
