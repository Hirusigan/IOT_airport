import axios from 'axios';
import { apiClient } from './apiClient';
import type { LuggageDetails } from '../types/LuggageDetails';
import type { LuggageState } from '../types/LuggageState';
import type { Observation } from '../types/Observation';
import type { LuggageEventPayload } from '../types/LuggageEventPayload';
import type { Trip } from '../types/Trip';
import type { Flight } from '../types/Flight';
export const luggageService = {
  async getDetails(id: string, signal?: AbortSignal): Promise<LuggageDetails> {
    try {
      const { data } = await apiClient.get<{
        states: LuggageState[];
        events: Observation[];
        trips: Trip[];
        flights: Flight[];
      }>(`/luggage/${encodeURIComponent(id)}`, { signal });
      return {
        states: data.states,
        trips: data.trips,
        flights: data.flights,
        events: data.events.map((event) => {
          const payload: LuggageEventPayload = JSON.parse(event.payload);
          return {
            ...event,
            stage: payload.data.stage,
            sequence: payload.data.sequence,
            flightId: payload.data.flightId ?? null,
          };
        }),
      };
    } catch (error) {
      if (axios.isAxiosError(error) && error.response?.status === 404)
        throw new Error('No matching luggage found.');
      throw error;
    }
  },
};
