// Plain DTOs mirroring the backend's Api/Contracts/V1 records.

export interface SubmitOrderRequest {
  userId: string;
  payableAmount: number;
  currencyCode: string;
  paymentGatewayId: string;
  description?: string;
}

export interface OrderReceiptResponse {
  orderNumber: string;
  paidAmount: number;
  currencyCode: string;
  paidAtUtc: string;
  paymentConfirmation: string;
}

export interface OrderErrorResponse {
  orderNumber: string;
  message: string;
}

export type OrderStatus = "Pending" | "Paid" | "Failed";

export interface OrderSummaryResponse {
  orderNumber: string;
  payableAmount: number;
  currencyCode: string;
  paymentGatewayId: string;
  description: string | null;
  status: OrderStatus;
  failureReason: string | null;
  receipt: OrderReceiptResponse | null;
  createdAtUtc: string;
}
