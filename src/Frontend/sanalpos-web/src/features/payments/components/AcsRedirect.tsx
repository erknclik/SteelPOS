import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { Spinner } from "@/shared/components/ui";
import { build3DSTermUrl, resolveAcsUrl } from "../api";

interface Props {
  acsUrl: string;
  md: string;
  paReq: string;
}

/**
 * 3D Secure ACS yönlendirmesi: kart hamilini bankanın doğrulama sayfasına klasik
 * form-post ile taşır (3DS 1.x akışı). ACS, doğrulama sonrası TermUrl'e (API'nin
 * complete endpoint'i) MD+PaRes post eder; API tarayıcıyı /payments/3ds/result
 * sayfasına geri yönlendirir.
 */
export function AcsRedirect({ acsUrl, md, paReq }: Props) {
  const { t } = useTranslation();
  const formRef = useRef<HTMLFormElement>(null);

  useEffect(() => {
    formRef.current?.submit();
  }, []);

  return (
    <div className="flex flex-col items-center gap-4 py-12 text-center">
      <Spinner />
      <p className="text-sm text-gray-600">{t("threeDs.redirecting")}</p>
      <form ref={formRef} method="post" action={resolveAcsUrl(acsUrl)}>
        <input type="hidden" name="MD" value={md} />
        <input type="hidden" name="PaReq" value={paReq} />
        <input type="hidden" name="TermUrl" value={build3DSTermUrl()} />
        <noscript>
          <button type="submit">{t("threeDs.continue")}</button>
        </noscript>
      </form>
    </div>
  );
}
