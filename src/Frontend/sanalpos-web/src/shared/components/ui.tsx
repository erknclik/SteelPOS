import { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode, SelectHTMLAttributes, forwardRef } from "react";
import clsx from "clsx";

// shadcn/ui benzeri hafif bileşen seti. İleri fazda shadcn/ui'a geçiş planlanmıştır
// (bkz. docs/09-frontend-react.md); arayüzler bilinçli olarak uyumlu tutuldu.

export function Button({
  className,
  variant = "primary",
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: "primary" | "secondary" | "danger" }) {
  return (
    <button
      className={clsx(
        "inline-flex items-center justify-center rounded-md px-4 py-2 text-sm font-medium transition-colors",
        "focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-600",
        "disabled:pointer-events-none disabled:opacity-50",
        variant === "primary" && "bg-brand-600 text-white hover:bg-brand-700",
        variant === "secondary" && "border border-gray-300 bg-white text-gray-700 hover:bg-gray-50",
        variant === "danger" && "bg-red-600 text-white hover:bg-red-700",
        className
      )}
      {...props}
    />
  );
}

export const Input = forwardRef<HTMLInputElement, InputHTMLAttributes<HTMLInputElement>>(
  function Input({ className, ...props }, ref) {
    return (
      <input
        ref={ref}
        className={clsx(
          "block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm",
          "focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500",
          className
        )}
        {...props}
      />
    );
  }
);

export const Select = forwardRef<HTMLSelectElement, SelectHTMLAttributes<HTMLSelectElement>>(
  function Select({ className, ...props }, ref) {
    return (
      <select
        ref={ref}
        className={clsx(
          "block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm",
          "focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500",
          className
        )}
        {...props}
      />
    );
  }
);

export function Field({ label, error, children }: { label: string; error?: string; children: ReactNode }) {
  return (
    <label className="block">
      <span className="mb-1 block text-sm font-medium text-gray-700">{label}</span>
      {children}
      {error && <span className="mt-1 block text-xs text-red-600">{error}</span>}
    </label>
  );
}

export function Card({ title, children, className }: { title?: string; children: ReactNode; className?: string }) {
  return (
    <section className={clsx("rounded-lg border border-gray-200 bg-white p-5 shadow-sm", className)}>
      {title && <h2 className="mb-4 text-base font-semibold text-gray-900">{title}</h2>}
      {children}
    </section>
  );
}

const statusColors: Record<string, string> = {
  Approved: "bg-green-100 text-green-800",
  Pending: "bg-yellow-100 text-yellow-800",
  Declined: "bg-red-100 text-red-800",
  Reversed: "bg-gray-200 text-gray-700",
  Refunded: "bg-blue-100 text-blue-800",
  PartiallyRefunded: "bg-blue-50 text-blue-700",
  Active: "bg-green-100 text-green-800",
  Suspended: "bg-red-100 text-red-800",
};

export function StatusBadge({ status }: { status: string }) {
  return (
    <span
      className={clsx(
        "inline-flex rounded-full px-2 py-0.5 text-xs font-medium",
        statusColors[status] ?? "bg-gray-100 text-gray-700"
      )}
    >
      {status}
    </span>
  );
}

export function Spinner() {
  return (
    <div className="flex justify-center p-8" role="status" aria-label="loading">
      <div className="h-8 w-8 animate-spin rounded-full border-4 border-gray-200 border-t-brand-600" />
    </div>
  );
}

export function Table({ headers, children }: { headers: string[]; children: ReactNode }) {
  return (
    <div className="overflow-x-auto">
      <table className="min-w-full divide-y divide-gray-200 text-sm">
        <thead>
          <tr>
            {headers.map((h) => (
              <th key={h} className="px-3 py-2 text-left text-xs font-semibold uppercase tracking-wide text-gray-500">
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">{children}</tbody>
      </table>
    </div>
  );
}
