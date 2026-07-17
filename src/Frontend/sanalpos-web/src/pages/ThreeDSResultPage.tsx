import { Link, useSearchParams } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Button, Card, StatusBadge } from "@/shared/components/ui";

/**
 * 3D Secure dönüş sayfası: ACS -> API complete callback'i tarayıcıyı buraya yönlendirir
 * (?transactionId=...&status=Approved|Declined|SessionExpired).
 */
export function ThreeDSResultPage() {
  const { t } = useTranslation();
  const [params] = useSearchParams();
  const transactionId = params.get("transactionId");
  const status = params.get("status") ?? "SessionExpired";
  const isApproved = status === "Approved";

  return (
    <div className="mx-auto max-w-lg pt-8">
      <Card>
        <div className="flex flex-col items-center gap-4 py-6 text-center">
          <span
            className={`flex h-14 w-14 items-center justify-center rounded-full text-2xl ${
              isApproved ? "bg-green-100 text-green-700" : "bg-red-100 text-red-700"
            }`}
          >
            {isApproved ? "✓" : "✕"}
          </span>

          <h1 className="text-xl font-semibold">
            {isApproved ? t("threeDs.approvedTitle") : t("threeDs.failedTitle")}
          </h1>

          {status !== "SessionExpired" ? (
            <StatusBadge status={status} />
          ) : (
            <p className="text-sm text-gray-600">{t("threeDs.sessionExpired")}</p>
          )}

          <div className="mt-2 flex gap-2">
            {transactionId && (
              <Link to={`/payments/${transactionId}`}>
                <Button variant="secondary">{t("threeDs.viewTransaction")}</Button>
              </Link>
            )}
            <Link to="/payments/new">
              <Button>{t("nav.newPayment")}</Button>
            </Link>
          </div>
        </div>
      </Card>
    </div>
  );
}
