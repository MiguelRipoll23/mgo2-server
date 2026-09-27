// The team rendering of the event testing tools page.
//
// It is a file of its own because the page draws the teams, the state menus and
// the pairings with rules a moderator reads differently from the raw rows: a
// team's leader, how full it is and which state the entry pipeline holds it in,
// and a pairing whose room may not exist yet. That logic is kept together here
// rather than spread through the page's event handlers.
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

  // The pairing states, which say what a match is waiting for rather than what
  // the client does with it: a pairing is only announced to its teams once a
  // room has been leased, so "paired" and "assigned" are the difference between
  // a match that is stuck and one that is about to start.
  const matchStates = [
    { value: 1, label: "Paired — waiting for a host room" },
    { value: 2, label: "Assigned — a room is holding it" },
  ];

  const byId = (id) => document.getElementById(id);

  // A team's name in a menu, with how full it is and the state the pipeline
  // holds it in, so a moderator can pick the right one without opening it.
  function label(team) {
    return `${team.name} — ${team.members}/${maximumPlayers}, state ${team.state}`;
  }

  // A row of the team list. The optional actions are the buttons that act on
  // this team; the page passes the Remove that deletes the row.
  function row(team, actions) {
    const element = document.createElement("div");
    element.className =
      "flex items-center justify-between gap-3 rounded-lg border border-slate-700 bg-slate-800/60 px-4 py-3";

    const detail = document.createElement("div");
    const name = document.createElement("p");
    name.className = "text-sm font-semibold text-slate-100";
    name.textContent = team.name;

    const meta = document.createElement("p");
    meta.className = "text-xs text-slate-500";
    meta.textContent =
      `led by ${team.leaderName} · ${team.members}/${maximumPlayers} players · state ${team.state}`;

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

  // A menu of every team this lobby holds, plus the escape hatch when the page
  // offers one. The selection is kept when the team is still there, so a
  // refresh does not silently change what a moderator picked and is about to
  // act on.
  function fillMenu(select, teamList, otherOption) {
    const chosen = select.value;
    select.replaceChildren();

    for (const team of teamList) {
      select.append(new Option(label(team), team.name));
    }

    if (otherOption) {
      select.append(new Option(otherOption, "other"));
    }

    if (teamList.some((team) => team.name === chosen)) {
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

  // Builds the Remove button of one team. It is passed in rather than wired
  // here so that this file never learns how a team is removed.
  function removeButton(name, onRemove) {
    const button = document.createElement("button");
    button.type = "button";
    button.className =
      "rounded-lg border border-red-500/40 px-3 py-1.5 text-xs font-semibold text-red-300 transition hover:bg-red-500/10";
    button.textContent = "Remove";
    button.addEventListener("click", () => onRemove(name));
    return button;
  }

  // One half of a pairing, named the way a team of that kind is named in a menu:
  // a stored team and a testing one are different things paired against each
  // other, and a row that hid that would be read as two real teams playing.
  function matchSide(side) {
    return `${side.name} — ${side.members}/${maximumPlayers}, ${side.isFake ? "made here" : "stored"}`;
  }

  // A pairing row. The room is named when there is one, because the name is the
  // only thing that says which room is holding the match, and its absence is
  // the answer to "why has nothing happened yet".
  function matchRow(match) {
    const element = document.createElement("div");
    element.className =
      "flex items-center justify-between gap-3 rounded-lg border border-slate-700 bg-slate-800/60 px-4 py-3";

    const detail = document.createElement("div");
    const name = document.createElement("p");
    name.className = "text-sm font-semibold text-slate-100";
    name.textContent = `Match ${match.identifier}`;

    const state = matchStates.find((candidate) => candidate.value === match.state);
    const room = match.room ? ` in ${match.room}` : "";
    const meta = document.createElement("p");
    meta.className = "text-xs text-slate-500";
    meta.textContent =
      `${matchSide(match.first)} against ${matchSide(match.second)} — ` +
      `${state ? state.label.toLowerCase() : `state ${match.state}`}${room}.`;

    detail.append(name, meta);
    element.append(detail);

    return element;
  }

  window.EventToolTeams = {
    maximumPlayers,
    teamStates,
    memberStates,
    matchStates,
    byId,
    label,
    row,
    matchRow,
    emptyList,
    fillMenu,
    fillStates,
    removeButton,
  };
})();
