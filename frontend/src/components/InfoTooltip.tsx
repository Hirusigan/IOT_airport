import { useId, type ReactNode } from 'react';
import { IoIosInformationCircleOutline } from 'react-icons/io';
import './styles/InfoTooltip.css';

export function InfoTooltip({
  label,
  children,
}: {
  label: string;
  children: ReactNode;
}) {
  const tooltipId = useId();

  return (
    <span className="info-tooltip">
      <button
        type="button"
        className="info-tooltip-trigger"
        aria-label={label}
        aria-describedby={tooltipId}
      >
        <IoIosInformationCircleOutline aria-hidden="true" focusable="false" />
      </button>
      <span className="info-tooltip-content" id={tooltipId} role="tooltip">
        {children}
      </span>
    </span>
  );
}
