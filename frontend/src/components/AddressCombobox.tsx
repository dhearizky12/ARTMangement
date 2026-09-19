import { useEffect, useId, useRef, useState } from "react";
import { wilayahApi, type VillageResult } from "../api/wilayahApi";
export function AddressCombobox({
  onChange,
  required = true,
}: {
  onChange: (value: VillageResult | null) => void;
  required?: boolean;
}) {
  const id = useId();
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<VillageResult[]>([]);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [active, setActive] = useState(-1);
  const selected = useRef(false);
  useEffect(() => {
    let stale = false;
    setResults([]);
    setActive(-1);
    setError("");
    if (selected.current || query.trim().length < 2) {
      setLoading(false);
      return;
    }
    setLoading(true);
    const timer = setTimeout(() => {
      wilayahApi
        .search(query)
        .then((data) => {
          if (!stale) setResults(data);
        })
        .catch(() => {
          if (!stale) setError("Pencarian gagal. Coba ketik kembali.");
        })
        .finally(() => {
          if (!stale) setLoading(false);
        });
    }, 300);
    return () => {
      stale = true;
      clearTimeout(timer);
    };
  }, [query]);
  function choose(item: VillageResult) {
    selected.current = true;
    setQuery(item.displayLabel);
    onChange(item);
    setOpen(false);
  }
  return (
    <div
      className="field address-combobox"
      onBlur={(e) => {
        if (!e.currentTarget.contains(e.relatedTarget)) setOpen(false);
      }}
    >
      <label htmlFor={id}>Kelurahan / desa</label>
      <input
        id={id}
        className="input"
        role="combobox"
        aria-autocomplete="list"
        aria-expanded={open}
        aria-controls={`${id}-list`}
        aria-activedescendant={
          open && active >= 0 ? `${id}-${active}` : undefined
        }
        autoComplete="off"
        placeholder="Cari nama kelurahan atau desa"
        value={query}
        required={required}
        onFocus={() => setOpen(true)}
        onChange={(e) => {
          selected.current = false;
          onChange(null);
          setQuery(e.target.value);
          setOpen(true);
        }}
        onKeyDown={(e) => {
          if (e.key === "Escape") {
            setOpen(false);
            return;
          }
          if (e.key === "ArrowDown" || e.key === "ArrowUp") {
            e.preventDefault();
            setOpen(true);
            setActive((index) =>
              results.length
                ? index < 0
                  ? e.key === "ArrowDown"
                    ? 0
                    : results.length - 1
                  : (index +
                      (e.key === "ArrowDown" ? 1 : -1) +
                      results.length) %
                    results.length
                : -1,
            );
          }
          if (e.key === "Enter" && open) {
            e.preventDefault();
            if (active >= 0 && results[active]) choose(results[active]);
          }
        }}
      />
      <small>Ketik minimal 2 huruf, lalu pilih wilayah yang sesuai.</small>
      {open && (
        <div className="address-results">
          <div role="status">
            {loading
              ? "Mencari wilayah…"
              : error ||
                (query.trim().length >= 2 &&
                !results.length &&
                !selected.current
                  ? "Alamat tidak ditemukan, coba kata kunci lain"
                  : "")}
          </div>
          <ul
            id={`${id}-list`}
            role="listbox"
            aria-label="Hasil pencarian wilayah"
          >
            {results.map((item, index) => (
              <li
                id={`${id}-${index}`}
                key={item.villageId}
                role="option"
                aria-selected={index === active}
                ref={(element) => {
                  if (index === active)
                    element?.scrollIntoView({ block: "nearest" });
                }}
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => choose(item)}
              >
                {item.displayLabel}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
