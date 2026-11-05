import { useEffect, useRef, useState } from 'react';
import type { MapPosition } from '../types/MapPosition';

export function useLuggageAnimation(tripId: string, positionKey: string, target: MapPosition | null) {
  const [position, setPosition] = useState<MapPosition | null>(target);
  const current = useRef(target);
  const previousTrip = useRef(tripId);
  const previousKey = useRef(positionKey);
  useEffect(() => {
    const from = current.current;
    const changedTrip = previousTrip.current !== tripId;
    const changedObservation = previousKey.current !== positionKey;
    previousTrip.current = tripId;
    previousKey.current = positionKey;
    if (changedTrip || !from || !target || !changedObservation || window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
      current.current = target; setPosition(target); return;
    }
    let frame = 0;
    const started = performance.now();
    const animate = (now: number) => {
      const progress = Math.min((now - started) / 1800, 1);
      const t = progress * progress * (3 - 2 * progress);
      const next = {
        latitude: from.latitude + (target.latitude - from.latitude) * t,
        longitude: from.longitude + (target.longitude - from.longitude) * t,
        offsetX: from.offsetX + (target.offsetX - from.offsetX) * t,
        offsetY: from.offsetY + (target.offsetY - from.offsetY) * t - Math.sin(Math.PI * t) * 10,
      };
      current.current = next; setPosition(next);
      if (progress < 1) frame = requestAnimationFrame(animate);
    };
    frame = requestAnimationFrame(animate);
    return () => cancelAnimationFrame(frame);
  }, [tripId, positionKey, target?.latitude, target?.longitude, target?.offsetX, target?.offsetY]);
  return position;
}
