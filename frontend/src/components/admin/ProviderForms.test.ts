import { describe, expect, it } from "vitest";
import {
  REQUIRED_DOCUMENT_TYPES,
  isSectionDirty,
  missingDocumentType,
} from "./ProviderForms";
import type { ProviderAdmin } from "../../api/marketplaceApi";

const data = {} as ProviderAdmin;

describe("missingDocumentType", () => {
  it("lists the required document types in order", () => {
    expect(REQUIRED_DOCUMENT_TYPES).toEqual(["KTP", "KK"]);
  });
  it("prefers KTP when no documents are stored", () => {
    expect(missingDocumentType([])).toBe("KTP");
  });
  it("prefers KK when KTP is already stored", () => {
    expect(missingDocumentType([{ documentType: "KTP" }])).toBe("KK");
  });
  it("falls back to KTP when both are stored", () => {
    expect(
      missingDocumentType([{ documentType: "KTP" }, { documentType: "KK" }]),
    ).toBe("KTP");
  });
});

describe("isSectionDirty for documents", () => {
  it("is clean for a pristine draft", () => {
    expect(
      isSectionDirty("documents", { documentType: "KTP", fileName: "" }, data),
    ).toBe(false);
  });
  it("is dirty once a file is picked", () => {
    expect(
      isSectionDirty(
        "documents",
        { documentType: "KK", fileName: "kk.png" },
        data,
      ),
    ).toBe(true);
  });
  it("stays clean when only the document type changed", () => {
    expect(
      isSectionDirty("documents", { documentType: "KK", fileName: "" }, data),
    ).toBe(false);
  });
});
