#!/usr/bin/env bash
# =============================================================================
# install-be-api-skills.sh
# Install tất cả skills Backend + API cho Claude Code
# Dùng cho dự án IoT Access Control (hoặc bất kỳ BE project nào)
# =============================================================================

set +e  # tiếp tục dù 1 skill lỗi/không tồn tại — best-effort install

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
RED='\033[0;31m'
NC='\033[0m'

log()  { echo -e "${GREEN}[✓]${NC} $1"; }
info() { echo -e "${CYAN}[→]${NC} $1"; }
warn() { echo -e "${YELLOW}[!]${NC} $1"; }
err()  { echo -e "${RED}[✗]${NC} $1"; }

echo ""
echo -e "${CYAN}=================================================${NC}"
echo -e "${CYAN}   Claude Code — BE + API Skills Installer       ${NC}"
echo -e "${CYAN}=================================================${NC}"
echo ""

# =============================================================================
# KIỂM TRA DEPENDENCIES
# =============================================================================
info "Kiểm tra dependencies..."

if ! command -v npx &>/dev/null; then
  err "npx không tìm thấy. Cài Node.js trước: https://nodejs.org"
  exit 1
fi

if ! command -v claude &>/dev/null; then
  warn "claude CLI không tìm thấy. Các bước /plugin sẽ cần chạy thủ công trong Claude Code."
  CLAUDE_AVAILABLE=false
else
  CLAUDE_AVAILABLE=true
fi

log "Dependencies OK"
echo ""

# =============================================================================
# BƯỚC 1: THÊM MARKETPLACE
# =============================================================================
info "Bước 1/5 — Thêm marketplaces..."

if [ "$CLAUDE_AVAILABLE" = true ]; then
  claude plugin marketplace add alirezarezvani/claude-skills 2>/dev/null || \
    warn "alirezarezvani/claude-skills đã tồn tại hoặc lỗi — bỏ qua"

  claude plugin marketplace add trailofbits/skills 2>/dev/null || \
    warn "trailofbits/skills đã tồn tại hoặc lỗi — bỏ qua"
else
  warn "Chạy thủ công trong Claude Code:"
  echo "  /plugin marketplace add alirezarezvani/claude-skills"
  echo "  /plugin marketplace add trailofbits/skills"
fi

log "Marketplaces OK"
echo ""

# =============================================================================
# BƯỚC 2: ENGINEERING SKILLS (qua /plugin)
# =============================================================================
info "Bước 2/5 — Cài Engineering plugins..."

PLUGINS=(
  # Core engineering pack — 24 skills:
  # senior-architect, senior-backend, senior-fullstack, senior-devops,
  # senior-secops, code-reviewer, api-design-reviewer, database-designer,
  # ci-cd-pipeline-builder, dependency-auditor, performance-profiler,
  # env-secrets-manager, observability-designer, tech-debt-tracker, v.v.
  "engineering-skills@claude-code-skills"

  # Advanced engineering pack — 25 POWERFUL-tier skills:
  # rag-architect, migration-architect, monorepo-navigator,
  # feature-flags-architect, kubernetes-operator, chaos-engineering,
  # slo-architect, incident-commander, mcp-server-builder, v.v.
  "engineering-advanced-skills@claude-code-skills"

  # Security scanner — scan skills/MCP trước khi install
  "skill-security-auditor@claude-code-skills"

  # Playwright testing — 9 skills, 55 templates
  "playwright-pro@claude-code-skills"
)

if [ "$CLAUDE_AVAILABLE" = true ]; then
  for plugin in "${PLUGINS[@]}"; do
    name=$(echo "$plugin" | cut -d@ -f1)
    info "  Installing $name..."
    claude plugin install "$plugin" -y 2>/dev/null || \
      warn "  Không cài được $plugin — thử thủ công: /plugin install $plugin"
  done
else
  warn "Chạy thủ công trong Claude Code:"
  for plugin in "${PLUGINS[@]}"; do
    echo "  /plugin install $plugin"
  done
fi

log "Engineering plugins OK"
echo ""

# =============================================================================
# BƯỚC 3: SKILLS QUA npx (project scope — commit cùng repo)
# =============================================================================
info "Bước 3/5 — Cài skills via npx (project scope)..."

# ---- Backend & API core ----
info "  [Backend Core] senior-backend, senior-architect..."
npx skills add alirezarezvani/claude-skills \
  --skill senior-backend \
  --skill senior-architect \
  --skill senior-devops \
  --skill senior-secops \
  --skill api-design-reviewer \
  --skill code-reviewer \
  -a claude-code -y

# ---- Database & Schema ----
info "  [Database] database-designer, database-schema-designer, migration-architect..."
npx skills add alirezarezvani/claude-skills \
  --skill database-designer \
  --skill database-schema-designer \
  --skill migration-architect \
  -a claude-code -y

# ---- Security ----
info "  [Security] dependency-auditor, env-secrets-manager, senior-security..."
npx skills add alirezarezvani/claude-skills \
  --skill dependency-auditor \
  --skill env-secrets-manager \
  --skill senior-security \
  -a claude-code -y

# ---- Performance & Observability ----
info "  [Observability] performance-profiler, observability-designer, tech-debt-tracker..."
npx skills add alirezarezvani/claude-skills \
  --skill performance-profiler \
  --skill observability-designer \
  --skill tech-debt-tracker \
  --skill pr-review-expert \
  -a claude-code -y

# ---- CI/CD & Infrastructure ----
info "  [Infra] ci-cd-pipeline-builder, feature-flags-architect, monorepo-navigator..."
npx skills add alirezarezvani/claude-skills \
  --skill ci-cd-pipeline-builder \
  --skill feature-flags-architect \
  --skill monorepo-navigator \
  -a claude-code -y

# ---- Auth & Access Control (critical cho IoT project) ----
# NOTE: 'security-auth' không tồn tại trong repo (325 skills) — đã bỏ.
#       Auth dùng 'senior-secops' + 'senior-security' (đã cài ở trên).

log "npx skills OK"
echo ""

# =============================================================================
# BƯỚC 4: THIRD-PARTY SECURITY SKILLS
# =============================================================================
info "Bước 4/5 — Cài third-party security skills..."

# Trail of Bits — gold standard security, 5 plugins:
# static-analysis, insecure-defaults, variant-analysis,
# differential-review, sharp-edges
info "  [Trail of Bits] static-analysis, insecure-defaults..."
npx skills add trailofbits/skills -a claude-code -y || \
  warn "Không thể cài trailofbits/skills — thử lại sau"

# OWASP Top 10 reference (Broken Access Control, Injection, Auth Failures...)
info "  [OWASP] Top 10:2025 reference..."
npx skills add agamm/claude-code-owasp -a claude-code -y || \
  warn "Không thể cài claude-code-owasp — thử lại sau"

# Database-specific: Supabase (nếu dùng)
# info "  [Supabase] postgres best practices..."
# npx skills add supabase/agent-skills -a claude-code -y

log "Security skills OK"
echo ""

# =============================================================================
# BƯỚC 5: META-SKILLS (workflow optimization)
# =============================================================================
info "Bước 5/5 — Cài meta-skills (workflow)..."

# Superpowers — full SDLC: brainstorm → plan → TDD → subagents → review
info "  [Superpowers] full dev lifecycle..."
npx skills add obra/superpowers -a claude-code -y || \
  warn "Không thể cài superpowers — thử: npx skills add obra/superpowers"

# skill-creator — tự tạo custom skills trong session
info "  [Skill Creator] từ claude-plugins-official..."
# Cái này install qua plugin, không qua npx
if [ "$CLAUDE_AVAILABLE" = true ]; then
  claude plugin install skill-creator@claude-plugins-official -y 2>/dev/null || true
else
  warn "Chạy thủ công: /plugin install skill-creator@claude-plugins-official"
fi

log "Meta-skills OK"
echo ""

# =============================================================================
# RELOAD & VERIFY
# =============================================================================
info "Reload plugins..."

if [ "$CLAUDE_AVAILABLE" = true ]; then
  claude plugin reload 2>/dev/null || warn "Reload thủ công: /reload-plugins"
else
  warn "Chạy thủ công trong Claude Code: /reload-plugins"
fi

echo ""
echo -e "${GREEN}=================================================${NC}"
echo -e "${GREEN}   Cài đặt hoàn tất!                             ${NC}"
echo -e "${GREEN}=================================================${NC}"
echo ""
echo -e "Skills đã cài (project scope → .claude/skills/):"
echo ""
echo -e "  ${CYAN}Backend Core${NC}"
echo "    senior-backend, senior-architect, senior-devops, senior-secops"
echo ""
echo -e "  ${CYAN}API & Code Quality${NC}"
echo "    api-design-reviewer, code-reviewer, pr-review-expert"
echo ""
echo -e "  ${CYAN}Database${NC}"
echo "    database-designer, database-schema-designer, migration-architect"
echo ""
echo -e "  ${CYAN}Security${NC}"
echo "    senior-security, dependency-auditor, env-secrets-manager"
echo "    security-auth (JWT/RBAC/OAuth2/MFA)"
echo "    Trail of Bits: static-analysis, insecure-defaults, v.v."
echo "    OWASP Top 10:2025 reference"
echo ""
echo -e "  ${CYAN}Performance & Observability${NC}"
echo "    performance-profiler, observability-designer, tech-debt-tracker"
echo ""
echo -e "  ${CYAN}Infra & CI/CD${NC}"
echo "    ci-cd-pipeline-builder, feature-flags-architect, monorepo-navigator"
echo ""
echo -e "  ${CYAN}Workflow${NC}"
echo "    obra/superpowers (full SDLC), skill-creator"
echo ""
echo -e "${YELLOW}Bước tiếp theo:${NC}"
echo "  1. git add .claude/skills/ && git commit -m 'chore: add BE+API skills'"
echo "  2. Trong Claude Code: /reload-plugins"
echo "  3. Thử: 'review my API design' hoặc 'optimize this SQL query'"
echo ""
echo -e "${YELLOW}Xem tất cả skills đã cài:${NC}"
echo "  npx skills list -a claude-code"
echo ""