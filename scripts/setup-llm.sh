#!/usr/bin/env bash
# Reads LLM_API_KEY (and optionally LLM_ENDPOINT, LLM_MODEL) from .env in the
# repo root, then writes ~/.devos/llm.json so all 4 DevOSRing plugins pick up
# the values automatically (no per-plugin button press required).

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ENV_FILE="$REPO_ROOT/.env"
OUT_DIR="$HOME/.devos"
OUT_FILE="$OUT_DIR/llm.json"

if [[ ! -f "$ENV_FILE" ]]; then
    echo "[setup-llm] $ENV_FILE not found. Copy .env.example to .env and set LLM_API_KEY." >&2
    exit 1
fi

# Source .env (only LLM_* lines) without leaking globs / arbitrary code.
LLM_API_KEY=""
LLM_ENDPOINT=""
LLM_MODEL=""
while IFS='=' read -r key value || [[ -n "$key" ]]; do
    [[ "$key" =~ ^[[:space:]]*# ]] && continue
    # strip surrounding quotes / whitespace from value
    value="${value#\"}"; value="${value%\"}"; value="${value#\'}"; value="${value%\'}"
    value="${value%$'\r'}"
    case "$key" in
        LLM_API_KEY)  LLM_API_KEY="$value" ;;
        LLM_ENDPOINT) LLM_ENDPOINT="$value" ;;
        LLM_MODEL)    LLM_MODEL="$value" ;;
    esac
done < "$ENV_FILE"

if [[ -z "$LLM_API_KEY" ]]; then
    echo "[setup-llm] LLM_API_KEY is empty in $ENV_FILE" >&2
    exit 1
fi

# Sensible defaults when only the key is provided. Groq is the assumed provider
# given the key shape (gsk_...) used in the demo; override via .env if needed.
if [[ -z "$LLM_ENDPOINT" ]]; then
    case "$LLM_API_KEY" in
        gsk_*)  LLM_ENDPOINT="https://api.groq.com/openai/v1" ;;
        sk-or*) LLM_ENDPOINT="https://openrouter.ai/api/v1" ;;
        sk-*)   LLM_ENDPOINT="https://api.openai.com/v1" ;;
        *)      LLM_ENDPOINT="https://api.openai.com/v1" ;;
    esac
fi

if [[ -z "$LLM_MODEL" ]]; then
    case "$LLM_ENDPOINT" in
        *groq*)        LLM_MODEL="llama-3.3-70b-versatile" ;;
        *openrouter*)  LLM_MODEL="openai/gpt-4o-mini" ;;
        *)             LLM_MODEL="gpt-4o-mini" ;;
    esac
fi

mkdir -p "$OUT_DIR"
chmod 700 "$OUT_DIR"

umask 077
tmp="$(mktemp "$OUT_FILE.XXXXXX")"
cat > "$tmp" <<EOF
{
  "endpoint": "$LLM_ENDPOINT",
  "model": "$LLM_MODEL",
  "apiKey": "$LLM_API_KEY",
  "systemPrompt": ""
}
EOF
chmod 600 "$tmp"
mv "$tmp" "$OUT_FILE"

echo "[setup-llm] Wrote $OUT_FILE"
echo "  endpoint: $LLM_ENDPOINT"
echo "  model:    $LLM_MODEL"
echo "  apiKey:   ${LLM_API_KEY:0:8}... (length ${#LLM_API_KEY})"
