# CI Setup (Unity PlayMode Tests)

This repository includes a GitHub Actions workflow:

- `.github/workflows/unity-playmode-tests.yml`

It runs Unity PlayMode tests on pull requests and on pushes to `main`/`master`.

## Required GitHub Secrets

Configure these repository secrets before enabling the workflow:

1. `UNITY_EMAIL`
2. `UNITY_PASSWORD`
3. `UNITY_LICENSE`

`UNITY_LICENSE` should be a valid Unity license content compatible with your editor version (`6000.3.10f1`).

## Notes

- The workflow caches `Library/` for faster runs.
- Test reports/artifacts are uploaded as `unity-playmode-results`.
- If secrets are missing, the workflow will fail during Unity activation.
