# Accessibility and Localization Checklist

Last updated: 2026-02-28
Owner: UX + QA
Status: PLAN_READY

Use this checklist before any production readiness claim.

## Accessibility Checklist
- Text readability:
- HUD labels remain readable at arm's length on 6" low-end device.
- Critical values (`state`, `meter`, `score`) stay legible in portrait and landscape.
- Contrast:
- Primary HUD text meets at least 4.5:1 contrast against effective background.
- Warning/danger colors are distinguishable without relying on hue alone.
- Colorblind resilience:
- Test in protanopia/deuteranopia simulation and verify danger vs normal cues are still separable.
- Motion/flash:
- No rapid full-screen flash effects that can trigger photosensitivity.
- Input clarity:
- Left-half move and right-half slow-time interactions are understandable from first run.
- Safe area:
- No key text overlaps notch/cutout/gesture regions.

## Localization Readiness Checklist
- Externalize user-visible strings to keyed resources (no hardcoded UI strings in gameplay scripts).
- Keep UI layouts stable for +30% string expansion.
- Avoid culturally ambiguous abbreviations in state labels.
- Score/number formatting uses locale-aware separators where appropriate.
- Support fallback font coverage for target languages.
- Validate truncation/overflow for longest localized strings.

## Minimal Test Matrix
- English baseline + one long-language test (e.g., German) + one non-Latin test.
- Small phone + large phone + tablet.

## Exit Criteria
- All checklist rows pass with evidence.
- Any exception has explicit mitigation and owner/date.

