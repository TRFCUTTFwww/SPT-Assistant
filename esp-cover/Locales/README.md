# Assistant localization

`en.json` contains manually authored English labels, help and messages. Keys retain the existing Chinese source text. `en-formats.json` contains complete dynamic sentences with numbered placeholders. Keep every placeholder and its meaning when editing a translation; word order may change.

`en-verbatim.json` identifies arguments containing user preset names, character identities or filenames. These arguments are copied unchanged even when their contents match a UI label. Other arguments may themselves contain catalog messages. Text fields and persisted identifiers never pass through localization.

Resources are embedded in the core module. `I18n` persists `zh-CN` / `en` in the existing interface section, updates settings metadata and refreshes the configuration manager on a language change. `InterfaceMenu` translates before drawing and measuring text; standalone HUD calls do the same. The base game's localization is left alone.

`LocalizationCatalog` matches only authored complete messages, with a bounded cache and regex timeouts. The linear matcher checks candidates; a bounded capture matcher preserves arguments, including trailing newlines. Unknown strings remain unchanged. Do not add network translation, word substitution or translation of user input.

Probe verifies English text, placeholder sets, every rendered format, unchanged custom text and verbatim name arguments. Run it against the current game build after editing catalogs. Compilation and offline checks do not establish in-game visual acceptance.
