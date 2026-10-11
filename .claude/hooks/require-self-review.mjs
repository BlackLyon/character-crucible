// PreToolUse gate: refuse `gh pr create` until the self-review has run.
//
// Exists because the rule was written down twice and broken twice -- see
// notes/process-rulesets-module.md. A convention without a mechanism is a reminder.
//
// Parses the payload rather than string-matching it. An earlier version matched the raw
// JSON, which denied any command merely MENTIONING `gh pr create` (grepping these files,
// or writing docs about them) and -- worse -- let any payload containing CC_REVIEW_DONE
// through, including this file's own deny text echoed back. The gate could disable itself.
//
// Node rather than a shell script: there is no jq on the author's machine, and Claude Code
// runs on Node, so it is the one interpreter guaranteed present wherever this hook runs.

import { execFileSync } from "node:child_process";
import { appendFileSync, readFileSync } from "node:fs";
import { join } from "node:path";

const projectDir = process.env.CLAUDE_PROJECT_DIR || process.cwd();
const allow = () => process.exit(0);

let command = "";
try {
  command = JSON.parse(readFileSync(0, "utf8"))?.tool_input?.command ?? "";
} catch {
  allow(); // Unparseable payload is not the author's problem; never block on it.
}

// `gh pr create` at a command position: start of input or after a separator, allowing any
// number of VAR=value prefixes. Quoted or interpolated mentions do not match.
const START = String.raw`(?:^|[\n;|&]\s*)`;
const ENV = String.raw`(?:[A-Za-z_][A-Za-z0-9_]*=\S*\s+)*`;
const GH = String.raw`gh\s+pr\s+create\b`;

if (!new RegExp(START + ENV + GH).test(command)) allow();

const git = (...args) =>
  execFileSync("git", args, { cwd: projectDir, encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).trim();

/** Files the PR would carry, or null if git cannot say -- which then fails closed. */
function changedFiles() {
  try {
    const base = (() => {
      try { return git("merge-base", "origin/main", "HEAD"); } catch { return git("merge-base", "main", "HEAD"); }
    })();
    // --no-renames: a .cs renamed to .md must show its deletion, not just the new name.
    return git("diff", "--name-only", "--no-renames", base, "HEAD").split("\n").filter(Boolean);
  } catch {
    return null;
  }
}

// Audit trail of every decision, so a skip can be checked after the fact. Gitignored, local.
function record(decision, files) {
  try {
    let branch = "", head = "";
    try { branch = git("rev-parse", "--abbrev-ref", "HEAD"); head = git("rev-parse", "--short", "HEAD"); } catch {}
    const entry = { at: new Date().toISOString(), decision, branch, head, files, command };
    appendFileSync(join(projectDir, ".claude", "review-gate.log"), JSON.stringify(entry) + "\n");
  } catch {
    // Never let the audit log decide the outcome.
  }
}

const files = changedFiles();

// Docs-only diffs cannot change what the app does. An empty list is not docs-only.
if (files && files.length > 0 && files.every((f) => f.toLowerCase().endsWith(".md"))) {
  record("auto-allow: docs only", files);
  allow();
}

// The override must be an env prefix on that same invocation, not merely present somewhere.
const OVERRIDE = String.raw`CC_REVIEW_DONE=\S+\s+`;
if (new RegExp(START + ENV + OVERRIDE + ENV + GH).test(command)) {
  record("override", files);
  allow();
}

record("deny", files);
process.stdout.write(JSON.stringify({
  hookSpecificOutput: {
    hookEventName: "PreToolUse",
    permissionDecision: "deny",
    permissionDecisionReason:
      "Self-review required before opening a PR that changes what runs or what it is allowed " +
      "to do: logic, service-to-service or environment config, security surface, build/test/CI " +
      "config, and major dependency upgrades. Run the self-code-review skill, fix or record every " +
      "finding, then re-run with CC_REVIEW_DONE=1 prefixed (Bash tool). Skippable: docs-only " +
      "diffs (auto-allowed), and patch/minor dependency bumps that build clean at -warnaserror " +
      "with the full suite passing. To skip, tell the user which category the change falls in " +
      "and why, and get their agreement before overriding; never override silently. " +
      "See notes/process-self-code-review.md.",
  },
}));
