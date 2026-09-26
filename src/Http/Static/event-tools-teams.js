// The team rendering of the event testing tools page.
//
// It is a file of its own because the page draws two lists that differ in one
// important way, and a moderator who cannot tell them apart waits for a
// pairing that can never happen: a real team is a row the entry pipeline
// queues, an in-memory team is not in that queue at all. So the kind of a team
// is part of how it is drawn and how it is named in a menu, and that logic is
// kept together here rather than spread through the page's event handlers.
//
// Everything is read from the DOM ids the page declares; nothing here talks to
// the server, which is what the page's own script does.
(function () {
  "use strict";

  // The largest roster a team can hold, which is what the counts are read
  // against. It is the same limit the server enforces.
  const maximumPlayers = 6;

  // The team states the client actually discriminates, with what each one
  // changes for a player. A value that is not listed here has no observed
  // meaning: the client reads the same branch for all of them, so a moderator
  // setting one is testing that the page works, not that the client does
  // something.
  const teamStates = [
    { value: 1, label: "Open — shown in the team list, joinable" },
    { value: 4, label: "A member was approved" },
    { value: 5, label: "Entry closed and a game assigned" },
    { value: 9, label: "Auto-formation — the client's waiting room" },
  ];

  // The member states, which the client paints as "OK" or "NG" and reads as
  // the accept/cancel toggle. Zero is the one value that is not a member state
  // at all: it tells the server to let each member follow the team's own.
  const memberStates = [
    { value: 0, label: "Let each member follow the team" },
    { value: 1, label: "Pending — painted NG, offers cancel entry" },
    { value: 2, label: "Ready — painted OK, offers accept entry" },
  ];

  const byId = (id) => document.getElementById(id);

  // A team's name in a menu has to say which kind it is, because the two kinds
  // answer to the same name and a menu that showed "Test" twice would fill an
  // arbitrary one of them.
  function label(team, kind) {
    const origin = kind === "real" ? "stored" : "in memory";
    return `${team.name} — ${team.members}/${maximumPlayers}, ${origin}, state ${team.state}`;
  }

  // A row of one of the two lists. The optional actions are the buttons that act
  // on this team, and which ones a team gets is the whole point of the split: a
  // stored team has no Remove, because forgetting one would be a deletion, and
  // an in-memory team has no pair action until it is written out.
  function row(team, kind, actions) {
    const element = document.createElement("div");
    element.className =
      "flex items-center justify-between gap-3 rounded-lg border border-slate-700 bg-slate-800/60 px-4 py-3";

    const detail = document.createElement("div");
    const name = document.createElement("p");
    name.className = "text-sm font-semibold text-slate-100";
    name.textContent = team.name;

    const meta = document.createElement("p");
    meta.className = "text-xs text-slate-500";
    meta.textContent = kind === "real"
      ? `led by ${team.leaderName} · ${team.members}/${maximumPlayers} players · state ${team.state}`
      : `${team.members}/${maximumPlayers} players · state ${team.state}`;

    detail.append(name, meta);
    element.append(detail);

    for (const action of actions || []) {
      element.append(action);
    }

    return element;
  }

  function emptyList(container, message) {
    const empty = document.createElement("p");
    empty.className =
      "rounded-lg border border-slate-700 bg-slate-800/60 px-4 py-3 text-sm text-slate-400";
    empty.textContent = message;
    container.replaceChildren(empty);
  }

  // A menu of every team, with the two kinds under their own headings. The
  // optgroup is what makes the distinction visible while scrolling rather than
  // only in the label each row carries.
  function fillMenu(select, realTeams, memoryTeams, otherOption) {
    const chosen = select.value;
    select.replaceChildren();

    if (realTeams.length > 0) {
      const stored = document.createElement("optgroup");
      stored.label = "Formed by a player — in the queue";
      for (const team of realTeams) {
        stored.append(new Option(label(team, "real"), team.name));
      }
      select.append(stored);
    }

    if (memoryTeams.length > 0) {
      const memory = document.createElement("optgroup");
      memory.label = "In this lobby's memory only — never queued";
      for (const team of memoryTeams) {
        memory.append(new Option(label(team, "memory"), team.name));
      }
      select.append(memory);
    }

    if (otherOption) {
      select.append(new Option(otherOption, "other"));
    }

    const available = [...realTeams, ...memoryTeams].some((team) => team.name === chosen);
    if (available) {
      select.value = chosen;
    }
  }

  // Fills a select with a list of labelled values plus the escape hatch. The
  // escape hatch is there because the client's byte range is wider than the
  // handful of values anybody has pinned a meaning to, and a moderator testing
  // a screen may need the value nobody thought to label.
  function fillStates(select, options, customLabel) {
    select.replaceChildren();
    for (const option of options) {
      select.append(new Option(option.label, String(option.value)));
    }
    select.append(new Option(customLabel, "custom"));
  }

  // Builds the Remove button of one in-memory team. It is passed in rather than
  // wired here so that this file never learns how a team is removed.
  function removeButton(name, onRemove) {
    const button = document.createElement("button");
    button.type = "button";
    button.className =
      "rounded-lg border border-red-500/40 px-3 py-1.5 text-xs font-semibold text-red-300 transition hover:bg-red-500/10";
    button.textContent = "Forget";
    button.addEventListener("click", () => onRemove(name));
    return button;
  }

  // Builds the action that turns one in-memory team into a team the queue can
  // pair. It is the only action on the page that writes a row, so it is worded
  // as what it does rather than as what it is: the team stops being a memory
  // team and becomes a stored one.
  function promoteButton(name, onPromote) {
    const button = document.createElement("button");
    button.type = "button";
    button.className =
      "rounded-lg border border-mgo-500/50 px-3 py-1.5 text-xs font-semibold text-sky-300 transition hover:bg-mgo-500/10";
    button.textContent = "Queue for pairing";
    button.title =
      "Writes this team out as a stored team and queues it, so a real team can be paired against it.";
    button.addEventListener("click", () => onPromote(name));
    return button;
  }

  window.EventToolTeams = {
    maximumPlayers,
    teamStates,
    memberStates,
    byId,
    label,
    row,
    emptyList,
    fillMenu,
    fillStates,
    removeButton,
    promoteButton,
  };
})();
