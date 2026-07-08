import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import type { DailySummary } from "@/types/api";
import { formatMoney } from "@/shared/lib/formatters";

export function DailySummaryChart({ summary }: { summary: DailySummary }) {
  const data = [
    { name: "Ciro", value: summary.totalAmount },
    { name: "Komisyon", value: summary.totalCommission },
    { name: "Net", value: summary.totalNet },
    { name: "İade", value: summary.totalRefunded },
  ];

  return (
    <div className="h-64 w-full">
      <ResponsiveContainer>
        <BarChart data={data} margin={{ top: 8, right: 8, bottom: 0, left: 8 }}>
          <CartesianGrid strokeDasharray="3 3" vertical={false} />
          <XAxis dataKey="name" tickLine={false} axisLine={false} />
          <YAxis tickLine={false} axisLine={false} width={80} tickFormatter={(v) => formatMoney(Number(v))} />
          <Tooltip formatter={(value) => formatMoney(Number(value))} />
          <Bar dataKey="value" fill="#4f6ef7" radius={[4, 4, 0, 0]} maxBarSize={64} />
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}
