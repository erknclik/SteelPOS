import { create } from "zustand";
import clsx from "clsx";

interface ToastItem {
  id: number;
  message: string;
  kind: "success" | "error";
}

interface ToastState {
  toasts: ToastItem[];
  push: (message: string, kind: ToastItem["kind"]) => void;
  remove: (id: number) => void;
}

let nextId = 1;

export const useToastStore = create<ToastState>((set) => ({
  toasts: [],
  push: (message, kind) => {
    const id = nextId++;
    set((s) => ({ toasts: [...s.toasts, { id, message, kind }] }));
    setTimeout(() => set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) })), 5000);
  },
  remove: (id) => set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) })),
}));

export const toast = {
  success: (message: string) => useToastStore.getState().push(message, "success"),
  error: (message: string) => useToastStore.getState().push(message, "error"),
};

export function ToastContainer() {
  const { toasts, remove } = useToastStore();
  return (
    <div className="fixed bottom-4 right-4 z-50 flex flex-col gap-2" aria-live="polite">
      {toasts.map((t) => (
        <button
          key={t.id}
          onClick={() => remove(t.id)}
          className={clsx(
            "rounded-md px-4 py-3 text-left text-sm text-white shadow-lg",
            t.kind === "success" ? "bg-green-600" : "bg-red-600"
          )}
        >
          {t.message}
        </button>
      ))}
    </div>
  );
}
