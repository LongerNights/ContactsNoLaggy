# ContactsNoLaggy

Makes contacts no laggy >:(

A ResoniteModLoader mod that removes the freeze when you open the Contacts tab for the first time after starting Resonite.

## What it changes

- **Faster UI material copies.** UI inside a scroll area makes a copy of each shared UI material it uses. Resonite keeps
  those shared materials on the userspace root, so every copy first searched the whole userspace (tens of thousands of
  slots). The contacts list makes hundreds of copies, which caused most of the freeze. The mod skips that search for
  material copies; the copied materials come out the same.
- **Contacts tab built in the background.** Once your contact list has loaded, the tab is built while hidden, at about
  2 ms per frame, so opening it doesn't have to build every row at once.

## Expected gains

Measured with about 600 contacts:

| First open of Contacts | Before | With mod |
|---|---|---|
| Freeze | about 8 to 10 s | one hitch of about 0.5 s |

The more contacts you have, the bigger the difference. Other scroll areas that copy shared materials also get faster.

## Notes

- The Contacts tab and its avatar thumbnails now load shortly after login even if you never open the tab, which costs
  a little memory.
- If you open Contacts within the first few seconds after login, before the background build finishes, the remaining
  rows are built right away, the same as without the mod.

## Install

Put `ContactsNoLaggy.dll` in `rml_mods` and restart Resonite.

To build: `dotnet build src -c Release -p:ResonitePath="<your Resonite folder>"`.
