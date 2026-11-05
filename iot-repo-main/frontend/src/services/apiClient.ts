import axios from 'axios';
export const apiClient = axios.create({ baseURL: '/api' });
export function requestError(error: unknown): string {
  if (axios.isAxiosError(error)) return error.response?.data?.error ?? (error.response ? `Request failed (${error.response.status}).` : 'Unable to reach the backend.');
  return error instanceof Error ? error.message : 'Request failed.';
}
export const isCanceled = axios.isCancel;
