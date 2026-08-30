# LocalLLM Hugging Face Download Design

## Goal
Allow a LocalLLM service to download a public `.gguf` model from a Hugging Face repository or direct file URL, show progress, and keep the transfer alive when Preferences is closed.

## Scope
- Accept a Hugging Face repository URL, `owner/repo`, or direct `/resolve/.../*.gguf` URL.
- Resolve repository URLs through the Hugging Face model API and expose only `.gguf` siblings.
- Download into `%LOCALAPPDATA%/DevBoard/models` (or the platform equivalent LocalApplicationData path).
- Stream to `<filename>.part`, then atomically rename to the final `.gguf` after success.
- Show percent, downloaded/total bytes, speed, ETA, status, Cancel and Retry.
- On success, assign the completed file to the selected LocalLLM service's `LocalModelPath`.
- A download is owned by an application-lifetime singleton, not by the Preferences window, so closing Preferences does not cancel it.
- v1 supports public Hugging Face models only; 401/403 are surfaced as errors.

## Architecture
`HuggingFaceModelDownloader` owns source parsing, repository discovery, active transfer state, cancellation, retry, and completion. `HuggingFaceDownloadPanel` is a compact programmatic Avalonia control injected into the existing LocalLLM Preferences editor from a partial `Preferences` class, avoiding duplication of the existing AI settings template.

## Safety and lifecycle
Only `https://huggingface.co` download/API endpoints are accepted. Partial files keep the `.part` suffix and are never selected as models. Cancel leaves the partial file for a retry; retry resumes when the server honors HTTP Range and otherwise restarts safely.
