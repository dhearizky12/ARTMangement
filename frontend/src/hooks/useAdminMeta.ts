import { useEffect } from "react";

export function useAdminMeta() {
  useEffect(() => {
    const previousTitle = document.title;
    document.title = "Admin · Bantu-Bantu";
    let meta = document.querySelector<HTMLMetaElement>('meta[name="robots"]');
    const previousContent = meta?.getAttribute("content") ?? null;
    if (!meta) {
      meta = document.createElement("meta");
      meta.setAttribute("name", "robots");
      document.head.appendChild(meta);
    }
    meta.setAttribute("content", "noindex,nofollow");
    return () => {
      document.title = previousTitle;
      if (meta) {
        if (previousContent === null) meta.remove();
        else meta.setAttribute("content", previousContent);
      }
    };
  }, []);
}