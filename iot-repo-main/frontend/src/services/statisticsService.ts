import { apiClient } from './apiClient';
import type { Statistics } from '../types/Statistics';
export const statisticsService = { async get(signal?: AbortSignal) { return (await apiClient.get<Statistics>('/statistics', { signal })).data; } };
