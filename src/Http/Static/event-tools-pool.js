// The test-character pool panel of the event testing tools page.
//
// The pool is global rather than per-lobby: its characters are rows in the
// characters table, owned by the server account, so it is listed and changed
// without asking any lobby. That is what separates it from the rest of the
// page, which is all about asking one lobby for the teams it holds, and it is
// why the panel lives in a file of its own.
//
// It makes two requests, both to the public pool endpoints. It is handed the
// page's status line rather than reaching for it, so this file never learns how
// a message is shown or what a refresh does.
(function () {
  "use strict";

  const maximumPerAdd = 32;

  function buildPanel(byId) {
    const section = document.createElement("section");
    section.className = "rounded-xl border border-slate-700 bg-slate-800/40 p-5";

    const heading = document.createElement("h2");
    heading.className = "mb-2 text-sm font-semibold text-slate-200";
    heading.textContent = "Test characters";

    const hint = document.createElement("p");
    hint.className = "mb-3 text-xs text-slate-500";
    hint.textContent =
      "Create and remove the characters teams are built from. They are owned by the server account, "
      + "and a team takes its players from here rather than making new ones.";

    const controls = document.createElement("div");
    controls.className = "mb-3 flex items-center gap-2";
    const count = document.createElement("input");
    count.type = "number";
    count.min = "1";
    count.max = String(maximumPerAdd);
    count.value = "3";
    count.className =
      "w-20 rounded-lg border border-slate-600 bg-slate-900 px-2 py-1 text-sm text-slate-100";
    const add = document.createElement("button");
    add.type = "button";
    add.textContent = "Add";
    add.className =
      "rounded-lg border border-mgo-500/50 px-3 py-1.5 text-xs font-semibold text-sky-300 transition hover:bg-mgo-500/10";
    controls.append(count, add);

    const list = document.createElement("div");
    list.id = "character-list";
    list.className = "flex flex-col gap-2";

    section.append(heading, hint, controls, list);

    const anchor = byId("real-team-list") && byId("real-team-list").closest("section");
    if (anchor && anchor.parentNode) {
      anchor.parentNode.insertBefore(section, anchor);
    } else {
      document.body.append(section);
    }

    return { list, add, count };
  }

  // Builds the panel and returns the one thing the page needs from it: a way to
  // paint the pool after a refresh.
  function create(options) {
    const showStatus = options.showStatus;
    const readMessage = options.readMessage;
    const refresh = options.refresh;
    const byId = window.EventToolTeams.byId;

    const panel = buildPanel(byId);
    panel.add.addEventListener("click", () => addCharacters(Number(panel.count.value)));

    function render(entries) {
      panel.list.replaceChildren();
      if (!entries || entries.length === 0) {
        const empty = document.createElement("p");
        empty.className = "text-xs text-slate-500";
        empty.textContent = "No test characters yet. Add some above.";
        panel.list.append(empty);
        return;
      }

      for (const character of entries) {
        const row = document.createElement("div");
        row.className =
          "flex items-center justify-between gap-3 rounded-lg border border-slate-700 bg-slate-900/40 px-3 py-2";
        const label = document.createElement("span");
        label.className = "text-sm text-slate-200";
        label.textContent =
          `${character.name} (${character.identifier}) — ${character.inTeam ? "in a team" : "free"}`;
        row.append(label);

        const remove = document.createElement("button");
        remove.type = "button";
        remove.textContent = "Delete";
        remove.className =
          "rounded-lg border border-red-500/40 px-3 py-1.5 text-xs font-semibold text-red-300 transition hover:bg-red-500/10";
        remove.addEventListener("click", () => deleteCharacter(character.identifier));
        row.append(remove);

        panel.list.append(row);
      }
    }

    async function addCharacters(count) {
      if (!Number.isInteger(count) || count < 1 || count > maximumPerAdd) {
        showStatus("error", `Add between 1 and ${maximumPerAdd} test characters.`);
        return;
      }

      showStatus("pending", `Adding ${count} test character(s)…`);
      const response = await fetch("/fake-teams/characters", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ count }),
      });

      if (!response.ok) {
        showStatus("error", await readMessage(response));
        return;
      }

      showStatus("success", `Added ${count} test character(s).`);
      await refresh();
    }

    async function deleteCharacter(identifier) {
      showStatus("pending", `Deleting test character ${identifier}…`);
      const response = await fetch("/fake-teams/characters/" + identifier, { method: "DELETE" });
      if (!response.ok) {
        showStatus("error", await readMessage(response));
        return;
      }

      showStatus("success", await readMessage(response));
      await refresh();
    }

    return { render };
  }

  window.EventToolPool = { create };
})();
