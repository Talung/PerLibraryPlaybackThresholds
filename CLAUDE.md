
# Project Development Rules

## Core Principle

This is an existing, working Jellyfin plugin. The objective is maintenance, refinement and improvement — not rebuilding or redesigning the project.

Preserving existing functionality is the highest priority.

## Strict Instructions

1. Follow the user's request exactly.
2. Do not make unrelated changes.
3. Do not redesign existing interfaces unless explicitly requested.
4. Do not rewrite working code unnecessarily.
5. Preserve all existing functionality.
6. Research Jellyfin API requirements before implementing unfamiliar functionality.
7. Make the smallest practical changes necessary to achieve the requested result.
8. Never assume an API, method or property exists. Verify it first.
9. Explain changes in plain English. The user is not a C# programmer.
10. If a requirement is unclear, ask before making assumptions.
11. Do not repeatedly revisit decisions that have already been agreed upon.
12. Do not introduce additional features or improvements without permission.

## Before Making Changes

- Inspect the relevant existing files before editing.
- Understand how the existing implementation works.
- Identify which files actually need changing.
- Check whether the requested change affects other parts of the plugin.
- Explain any potential side effects.
- If a change requires broader modifications than requested, explain why and seek approval first.

## While Making Changes

- Keep changes focused on the requested task.
- Follow existing coding conventions and project structure.
- Preserve existing naming conventions and architecture.
- Avoid unnecessary refactoring.
- Avoid replacing entire files when targeted modifications are sufficient.
- Do not remove existing functionality simply because a different implementation is preferred.
- Do not make speculative changes.

## Jellyfin-Specific Requirements

- Verify API compatibility against the Jellyfin version targeted by the plugin.
- Consult official Jellyfin documentation and source code when necessary.
- Do not invent API methods, interfaces, properties or configuration options.
- Maintain compatibility with the existing plugin framework.
- Consider Jellyfin's existing UI conventions when modifying interfaces.
- Preserve existing plugin configuration and user settings unless explicitly asked to change them.

## After Making Changes

1. Summarise exactly what was changed.
2. List the files modified.
3. Explain the changes in plain English.
4. Build the project where possible.
5. Report compilation errors honestly.
6. Identify anything that still needs testing.
7. Do not claim something works unless it has been verified.

## Communication Requirements

- Be concise and stay on topic.
- Avoid lengthy explanations unless requested.
- Do not overwhelm the user with unnecessary technical details.
- Do not repeatedly suggest alternative approaches after a decision has been made.
- Ask questions only when genuinely necessary.
- If something cannot be done, explain the specific limitation and suggest a practical solution.

## Scope Control

The user's explicit instructions always take priority over the AI's preferences.

Do not:
- Expand the scope of a task.
- Introduce unsolicited redesigns.
- Revisit completed work without a reason.
- Change unrelated code.
- Turn a small adjustment into a major refactoring exercise.

If you notice an unrelated problem, mention it separately without attempting to fix it.

## Final Priority

**Do exactly what was requested, change as little as possible, preserve what already works, and verify the result.**

The objective is to improve the existing plugin incrementally, not to create a different plugin.
