# Skill: Markdown Sequential Docs Navigation

**Category:** Documentation / Presenter Materials  
**Level:** Foundational  
**Author:** Lambert (DevRel)  
**Date:** 2026-04-20

---

## Overview

When building step-by-step setup or tutorial documentation for first-time users (especially presenters), sequential nav footers improve discoverability and reduce confusion about reading order. This skill documents the pattern and reusable template.

---

## Problem It Solves

- Readers don't know if there are more steps after the current doc
- No clear way to "go back" to a previous step without browser history
- First-time presenters running setup docs sequentially get lost
- Hard to understand the overall structure from individual doc views

---

## The Pattern

### Prerequisites
- Identify your doc sequence (e.g., Prerequisites → Installation → CloudSetup → LocalRun)
- Decide which docs are "on the main path" vs. "side references"
- Main path docs get full prev/next nav; side-refs get back-links only

### Nav Block Placement
- **Location:** Footer of each doc, after a `---` rule
- **Format:** Single line, markdown link syntax
- **Home link:** Always include (labeled 🏠 for visual consistency)

### Link Format

**First doc (start):**
```markdown
---

**Navigation:** [🏠 Session Delivery Resources](../readme.md) | [Next: <DocTitle> ➡](./<filename>.md)
```

**Middle docs:**
```markdown
---

**Navigation:** [⬅ Previous: <DocTitle>](./<filename>.md) | [🏠 Session Delivery Resources](../readme.md) | [Next: <DocTitle> ➡](./<filename>.md)
```

**Last doc (end):**
```markdown
---

**Navigation:** [⬅ Previous: <DocTitle>](./<filename>.md) | [🏠 Session Delivery Resources](../readme.md)
```

**Side-referenced doc (not on main path):**
```markdown
---

**Navigation:** [⬅ Back: <ParentDocTitle>](./<parent-filename>.md) | [🏠 Session Delivery Resources](../readme.md)
```

---

## Anchor Link Verification

When adding forward pointers to another doc (e.g., "See step X in doc Y"), verify the anchor:

1. **Find the target heading** in the destination doc
   - Example: `## 4) Create agents using the console application`

2. **Generate the anchor** (GitHub auto-generation rules):
   - Convert to lowercase
   - Replace spaces with hyphens
   - Remove punctuation
   - Result: `#4-create-agents-using-the-console-application`

3. **Test the link** in the rendered markdown or on GitHub

**Example forward pointer:**
```markdown
- ➡ **You'll run this console app as part of [Step 02: Create agents](./02.NeededCloudResources.md#4-create-agents-using-the-console-application).**
```

---

## Implementation Checklist

- [ ] Define your doc sequence (main path + side-refs)
- [ ] Add `---` separator before nav block in each doc
- [ ] Add nav footer links matching the pattern above
- [ ] Verify all internal links resolve
- [ ] For forward pointers, validate target anchors
- [ ] Update the docs index/table-of-contents (if one exists) to reflect the sequence
- [ ] Test reading flow end-to-end

---

## Example: BRK445 Setup Sequence

**Main path:**
1. Prerequisites.md
2. 01.Installation.md
3. 02.NeededCloudResources.md
4. 03.HowToRunDemoLocally.md

**Side reference:**
- ManualAgentDeployment.md (referenced from 02, but not on main path)

**Footer blocks added:**

| Doc | Footer |
|-----|--------|
| Prerequisites.md | `[🏠 Home](../readme.md) \| [Next: Installation ➡](./01.Installation.md)` |
| 01.Installation.md | `[⬅ Prev: Prerequisites](./Prerequisites.md) \| [🏠 Home](../readme.md) \| [Next: Cloud Resources ➡](./02.NeededCloudResources.md)` |
| 02.NeededCloudResources.md | `[⬅ Prev: Installation](./01.Installation.md) \| [🏠 Home](../readme.md) \| [Next: Run Locally ➡](./03.HowToRunDemoLocally.md)` |
| 03.HowToRunDemoLocally.md | `[⬅ Prev: Cloud Resources](./02.NeededCloudResources.md) \| [🏠 Home](../readme.md)` |
| ManualAgentDeployment.md | `[⬅ Back: Cloud Resources](./02.NeededCloudResources.md) \| [🏠 Home](../readme.md)` |

---

## Tips

- **Emojis:** Arrows (⬅ ➡) and home (🏠) icons are visual quick-scans; helps readability.
- **Consistent labels:** Use "Previous" / "Next" on main path; "Back" for side-refs.
- **Home always present:** Gives readers an escape hatch to the overview page.
- **Order:** Always [Previous] - [Home] - [Next] for predictability.
- **Before/after testing:** Read the sequence yourself end-to-end to catch missing links.

---

## When NOT to Use

- Single-doc guides (nav not needed)
- Docs that are truly independent (no reading order)
- API references or glossaries (random-access docs)

Use when: linear tutorial, step-by-step setup, numbered prerequisites, onboarding workflows.

---

## Future Improvements

- Auto-generate nav blocks from a YAML sequence config (for larger doc sets)
- Breadcrumb styling for web renderers
- Sidebar/toc integration in static site generators (MkDocs, Hugo, etc.)
