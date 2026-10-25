# UI guidelines

Use Jellyfin's native controls and theme. Keep the settings page practical, readable, and usable at desktop and phone widths without adding fonts, CDNs, or dependencies.

## Structure and controls

Keep HTML, scoped CSS, the controller, and focused ES modules separate. Register embedded resources through Jellyfin's configuration-page mechanism. Preserve reverse-proxy base paths when loading modules and calling APIs.

All panels share the available form width. Text, URL, password, and number inputs share one scoped height token; adjacent buttons match that height after native upgrade. Checkboxes retain native sizing. Put Discover folders beside the service URL and Remove beside its mapping pair, stacking controls on narrow screens.

Each remote/local mapping is one bordered group with a role label and one Remove control. Removing a translation never removes files. Keep URLs visible and credentials in collapsible details.

Jellyfin's component polyfill needs its native creation conventions. Preserve the existing parent-markup approach for dynamic inputs and retain the button class installed during upgrade.

## Editing and observability

Discovery uses the unsaved connection fields. Merge new suggestions without replacing manual translations. Suggestions remain unsaved until Save. Keep invalid rows editable, reject unsafe paths, and prevent a late load from overwriting edits or a failed load from enabling Save.

Distinguish manual folder checks from background download scans. Report loading, a safe failure, last successful check, and folder count. Configuration alone does not establish connectivity or local readability.

Refresh activity only while the page is active, prevent overlapping requests, and stop timers on hide or disposal. Show progress, readers, scan timing, empty states, and the bounded completion history. Native-item links open normal Jellyfin details.

Use short mouseover explanations for relevant fields, actions, mapping roles, and metrics. Keep keyboard-accessible descriptions. Apply the faint dashed underline only to explanatory text; clickable links and summaries keep normal pointer behavior.

Render returned titles and paths as text. Never put credentials or capability keys in errors, metrics, tooltips, or logs.

## Verification

Use the actual Jellyfin loader and native components. Check desktop at 1600 pixels and a narrow phone viewport, then leave the browser at desktop size. Measure input/button heights after upgrade, check overflow, and exercise discovery, manual overrides, add/remove, Save, reload, and lifecycle polling.
