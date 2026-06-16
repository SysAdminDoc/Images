# Blocked Roadmap Items

This file holds roadmap work that is real but not currently actionable by an
agent. Move an item back to `ROADMAP.md` only when its blocker is cleared.

## Blocked On Local GUI Or Human Runtime Smoke

- [ ] **V02-04** *P2* — Recapture main screenshot (DPI-aware, `PrintWindow(hwnd, hdc, 2)` per `screenshots.md`). Requires Windows GUI session.
  - **Blocked by**: interactive Windows GUI screenshot session.
  - **Unblock when**: a GUI session is available and screenshots can be captured/verified.

- [ ] **V02-05** *P2* — Human runtime smoke: prev/next/wrap, rename+undo, drag-drop, Del-to-recycle, external add/delete 250 ms roundtrip.
  - **Blocked by**: human runtime validation requirement.
  - **Unblock when**: the manual smoke pass is scheduled or can be replaced by an accepted automated UI smoke.

## Blocked On Package-Manager Credentials Or Account Setup

- [ ] **D-02** *P0* — **`winget` publishing** via `WinGet Releaser` GitHub Action (`vedantmgoyal9/winget-releaser`). First submission manual via `wingetcreate new`; subsequent releases auto-fire on `release: [published]`. Requires classic PAT + forked `microsoft/winget-pkgs`. Effort: S. [WinGet Releaser action; Grafana k6 PR #5203]
  - **Blocked by**: first manual `SysAdminDoc.Images` submission to `microsoft/winget-pkgs`, fork ownership, and classic `public_repo` PAT stored as `WINGET_TOKEN`.
  - **Unblock when**: the package exists in WinGet and the repository secret/fork are configured.

## Blocked On Code-Signing Identity Or External Approval

- [ ] **D-05** *P0* — **Azure Artifact Signing** (rebrand of Azure Trusted Signing, now GA April 2026) via `azure/artifact-signing-action` in the release workflow. SmartScreen reputation warm-up still applies (since 2023 even EV is throttled for new publishers) — so no reason to pay for EV. Self-employed individuals now eligible (no 3-yr history requirement); restricted to US/CA/EU/UK businesses/individuals. Effort: M. [[S-ARTIFACT-SIGNING]](https://azure.microsoft.com/en-us/products/artifact-signing) [[S-SMARTSCREEN-REGRESSION]](https://learn.microsoft.com/en-us/answers/questions/5855708/trusted-signing-regression-in-smartscreen-reputati) *Risk flagged 2026-03/04: Microsoft silently rotated issuing CAs (EOC CA 02 → AOC CA 03 → EOC CA 04) which broke SmartScreen reputation for existing customers. Expect the first ~500 installs to trip "Unrecognized app" even with a valid cert. Hanselman has the working GitHub-Actions setup [[S-HANSELMAN-SIGN]](https://www.hanselman.com/blog/automatically-signing-a-windows-exe-with-azure-trusted-signing-dotnet-sign-and-github-actions).*
  - **Blocked by**: Azure Artifact Signing account, certificate profile, tenant/app credentials, and repository secrets.
  - **Unblock when**: signing identity and GitHub Actions secrets are provisioned.

- [ ] **D-05a** *P1* — **SignPath.io OSS code-signing evaluation** (new, 2026-04-25 research). Free certificate via SignPath Foundation for OSS projects (used by PicView). Pre-requisite: GitHub Actions integration + SignPath-approved project status. Evaluate in parallel with D-05 — whichever lands first wins; both are fine to keep running simultaneously (dual-signing is supported by Authenticode). Effort: S (application) + M (pipeline). [[S-PV]](https://github.com/Ruben2776/PicView)
  - **Blocked by**: external SignPath Foundation application and approval.
  - **Unblock when**: project approval and integration credentials are available.

- [ ] **P-07** *P2* — **C2PA write-on-export** — stamp "edited with Images v0.x" + operation list on every export from v0.3/v0.5. Requires signing identity (Azure Trusted Signing works). Defers until P-05 is stable. Effort: M.
  - **Blocked by**: C2PA signing identity and certificate choice.
  - **Unblock when**: D-05 or another accepted signing identity is available.

- [ ] **V50-25** *P2* — **C2PA write-on-export** (P-07). Per-op, opt-in; embeds operation manifest + signing identity. Requires D-05 (Trusted Signing) for the cert.
  - **Blocked by**: D-05 signing certificate.
  - **Unblock when**: signing credentials are available and P-07 is active again.

- [ ] **V50-34** *P2* — **Configurable C2PA signing identity** — default to Azure Trusted Signing cert (D-05); allow user-supplied identity.
  - **Blocked by**: at least one approved signing identity path.
  - **Unblock when**: Azure Artifact Signing, SignPath, or another accepted signing path is provisioned.
