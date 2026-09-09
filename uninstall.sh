#!/bin/bash
set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

echo_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

# Offer credential cleanup while the CLI is still installed. The default is to
# preserve credentials, and environment-variable credentials are never changed.
if command -v trello-cli &> /dev/null; then
    echo ""
    REPLY=""
    read -r -p "Remove saved trello-cli credentials before uninstalling? (y/N) " REPLY || true
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        echo_info "Removing saved trello-cli credentials..."
        CLEAR_OUTPUT=""
        if CLEAR_OUTPUT="$(trello-cli --clear-auth)"; then
            CLEAR_STATUS=0
        else
            CLEAR_STATUS=$?
        fi
        printf '%s\n' "$CLEAR_OUTPUT"
        if [ "$CLEAR_STATUS" -eq 0 ] && [[ $CLEAR_OUTPUT == *'"ok":true'* ]]; then
            echo_info "Saved credentials removed"
        else
            echo_warn "Credential removal failed; the CLI will remain installed."
            exit 1
        fi
    else
        echo_info "Credentials preserved"
    fi
else
    echo_warn "trello-cli is not in PATH; saved credentials were not removed."
fi

# Uninstall the global tool
if dotnet tool list --global 2>/dev/null | grep -qi "trelloCli\|TrelloCli"; then
    echo_info "Uninstalling trello-cli global tool..."
    dotnet tool uninstall --global TrelloCli
else
    echo_warn "trello-cli global tool not found"
fi

# Remove Claude Code skills
CLAUDE_SKILLS_DIR="$HOME/.claude/skills/trello-cli"
if [ -d "$CLAUDE_SKILLS_DIR" ]; then
    echo_info "Removing Claude Code skills..."
    rm -rf "$CLAUDE_SKILLS_DIR"
    echo_info "Skills removed from $CLAUDE_SKILLS_DIR"
else
    echo_warn "Claude Code skills not found at $CLAUDE_SKILLS_DIR"
fi

# Remove config (optional - ask user)
CONFIG_DIR="$HOME/.trello-cli"
if [ -d "$CONFIG_DIR" ]; then
    echo ""
    read -p "Remove trello-cli config at $CONFIG_DIR? (y/N) " -n 1 -r
    echo ""
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        rm -rf "$CONFIG_DIR"
        echo_info "Config removed"
    else
        echo_info "Config preserved at $CONFIG_DIR"
    fi
fi

echo ""
echo_info "Uninstall complete!"
