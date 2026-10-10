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

import { readFileSync } from "node:fs";

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

// The override must be an env prefix on that same invocation, not merely present somewhere.
const OVERRIDE = String.raw`CC_REVIEW_DONE=\S+\s+`;
if (new RegExp(START + ENV + OVERRIDE + ENV + GH).test(command)) allow();

process.stdout.write(JSON.stringify({
  hookSpecificOutput: {
    hookEventName: "PreToolUse",
    permissionDecision: "deny",
    permissionDecisionReason:
      "Self-review required before opening a PR. This rule has already been broken twice " +
      "(PR #10, PR #12). Run the self-code-review skill on this diff, then fix or record " +
      "every finding, and re-run with CC_REVIEW_DONE=1 prefixed. Small or infrastructure-only " +
      "diffs are NOT exempt -- PR #12 was one Compose file and still had something a review " +
      "would have named. If the review genuinely does not apply, say so to the user and get " +
      "their agreement before overriding; never override silently.",
  },
}));
