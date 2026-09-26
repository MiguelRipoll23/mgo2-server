// The behaviour of the event testing tools page.
//
// Everything here talks to the public fake-team endpoints, so there is no token
// and nothing to sign in with. The page is served at both /survival and
// /tournament and is the same code either way: the path only picks the lobby
// the page opens on.
(function () {
  "use strict";

  // The largest roster a team can hold, which is what the count menus are built
  // from. It is the same limit the server enforces.
  const maximumPlayers = 6;

  // The team states worth offering by name. The rest of the byte range is
  // reachable through the custom entry, because the client reads it and a
  // moderator testing a screen may need a value nobody thought to label.
  const teamStates = [
    { value: 1, label: "Joinable — shows in the team list" },
    { value: 9, label: "Registered — queued for an opponent" },
    { value: 3, label: "Entering" },
    { value: 5, label: "Playing" },
    { value: 6, label: "Finished" },
  ];

  // The member states a moderator exercises with, plus the one value that means
  // "let each member follow the team".
  const memberStates = [
    { value: 0, label: "Follow the team" },
    { value: 1, label: "Pending — has not decided" },
    { value: 2, label: "Ready" },
    { value: 4, label: "Approved" },
    { value: 5, label: "Closed" },
    { value: 6, label: "Refused" },
  ];

  const byId = (id) => document.getElementById(id);

  const modeSelect = byId("mode");
  const status = byId("status");
  const statusText = byId("status-text");
  const teamList = byId("team-list");
  const playersTeam = byId("players-team");
  const playersTeamOther = byId("players-team-other");
  const playersTeamName = byId("players-team-name");
  const stateTeam = byId("state-team");

  // The teams the last listing returned, held so the menus and the list on the
  // page agree with each other without asking the lobby twice.
  let teams = [];

  function showStatus(kind, message) {
    statusText.textContent = message;
    statusText.className =
      kind === "error"
        ? "rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm font-medium text-red-300"
        : kind === "pending"
          ? "rounded-lg border border-slate-700 bg-slate-800/60 px-4 py-3 text-sm font-medium text-slate-300"
          : "rounded-lg border border-emerald-500/30 bg-emerald-500/10 px-4 py-3 text-sm font-medium text-emerald-300";
    status.classList.remove("hidden");
  }

  // Reads the sentence a refusal carries, so the page says what went wrong
  // rather than only that something did.
  async function readMessage(response) {
    const data = await response.json().catch(() => null);
    return (data && data.message) || response.statusText || "The request was refused.";
  }

  function fillStates(select, options, customLabel) {
    select.replaceChildren();
    for (const option of options) {
      select.append(new Option(option.label, String(option.value)));
    }
    select.append(new Option(customLabel, "custom"));
  }

  function fillCounts(select, chosen) {
    select.replaceChildren();
    for (let count = 1; count <= maximumPlayers; count++) {
      select.append(
        new Option(count === 1 ? "1 player" : count + " players", String(count))
      );
    }
    select.value = String(chosen);
  }

  // A team is named, so both menus that act on a team are built from the
  // listing rather than asking for a name nobody can check.
  function fillTeamMenu(select, otherOption) {
    const chosen = select.value;
    select.replaceChildren();
    for (const team of teams) {
      const label = `${team.name} — ${team.members}/${maximumPlayers}, state ${team.state}`;
      select.append(new Option(label, team.name));
    }
    if (otherOption) {
      select.append(new Option(otherOption, "other"));
    }
    if (teams.some((team) => team.name === chosen)) {
      select.value = chosen;
    }
  }

  function renderList() {
    teamList.replaceChildren();
    if (teams.length === 0) {
      const empty = document.createElement("p");
      empty.className = "rounded-lg border border-slate-700 bg-slate-800/60 px-4 py-3 text-sm text-slate-400";
      empty.textContent = "This lobby is holding no teams in memory.";
      teamList.append(empty);
      return;
    }

    for (const team of teams) {
      const row = document.createElement("div");
      row.className =
        "flex items-center justify-between gap-3 rounded-lg border border-slate-700 bg-slate-800/60 px-4 py-3";

      const detail = document.createElement("div");
      const name = document.createElement("p");
      name.className = "text-sm font-semibold text-slate-100";
      name.textContent = team.name;
      const meta = document.createElement("p");
      meta.className = "text-xs text-slate-500";
      meta.textContent = `${team.members}/${maximumPlayers} players · state ${team.state}`;
      detail.append(name, meta);

      const remove = document.createElement("button");
      remove.type = "button";
      remove.className =
        "rounded-lg border border-red-500/40 px-3 py-1.5 text-xs font-semibold text-red-300 transition hover:bg-red-500/10";
      remove.textContent = "Remove";
      remove.addEventListener("click", () => removeTeam(team.name));

      row.append(detail, remove);
      teamList.append(row);
    }
  }

  async function removeTeam(name) {
    showStatus("pending", `Asking the lobby to forget "${name}"…`);
    const response = await fetch(
      "/fake-teams/" + encodeURIComponent(name) + "?mode=" + modeSelect.value,
      { method: "DELETE" }
    );
    if (!response.ok) {
      showStatus("error", await readMessage(response));
      return;
    }
    showStatus("success", await readMessage(response));
    await refresh();
  }

  // Asking the lobby is the only way to see the teams: they live in that
  // process, so the page cannot work them out for itself.
  async function refresh() {
    showStatus("pending", "Asking the lobby what it is holding…");
    try {
      const response = await fetch("/fake-teams?mode=" + modeSelect.value);
      if (!response.ok) {
        teams = [];
        renderList();
        showStatus("error", await readMessage(response));
        return;
      }

      const data = await response.json();
      teams = data.teams || [];
      fillTeamMenu(playersTeam, "Another team a player formed…");
      fillTeamMenu(stateTeam, null);
      renderList();
      showStatus(
        "success",
        teams.length === 1
          ? "The lobby is holding 1 team."
          : `The lobby is holding ${teams.length} teams.`
      );
    } catch (error) {
      teams = [];
      renderList();
      showStatus("error", "Could not reach the server. Is it running?");
    }
  }

  async function send(path, body) {
    showStatus("pending", "Sending…");
    try {
      const response = await fetch(path, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });

      const message = await readMessage(response);
      showStatus(response.ok ? "success" : "error", message);
      if (response.ok) {
        await refresh();
      }
    } catch (error) {
      showStatus("error", "Could not reach the server. Is it running?");
    }
  }

  // A custom state is a number the menus do not name, so it is asked for
  // rather than guessed at.
  function customState(select) {
    const answer = window.prompt("Which state byte?", select.value);
    if (answer === null) {
      return null;
    }
    const value = Number(answer.trim());
    return Number.isInteger(value) && value >= 0 && value <= 255 ? value : null;
  }

  function onSubmit(id, handler) {
    byId(id).addEventListener("submit", (event) => {
      event.preventDefault();
      handler();
    });
  }

  // The page is the same code at both routes; the path picks the lobby the mode
  // select starts on, so the tournament page opens on registration and the
  // survival page on Survival.
  const isTournament = window.location.pathname.replace(/\/$/, "").endsWith("/tournament");
  byId("title").textContent = isTournament ? "Tournament" : "Survival";
  modeSelect.value = isTournament ? "10" : "4";
  byId("mode-hint").textContent = isTournament
    ? "Tournament tools, opening on the registration lobby. Change the lobby above if you meant another one."
    : "Survival tools. Change the lobby above if you meant another one.";

  fillCounts(byId("create-count"), maximumPlayers);
  fillCounts(byId("players-count"), 1);
  fillStates(byId("state-team-state"), teamStates, "Another state byte…");
  fillStates(byId("state-member"), memberStates, "Another member state byte…");

  onSubmit("create-form", () =>
    send("/fake-teams", {
      mode: Number(modeSelect.value),
      count: Number(byId("create-count").value),
      teamName: byId("create-team").value.trim(),
      playerPrefix: byId("create-prefix").value.trim(),
    })
  );

  onSubmit("players-form", () => {
    const chosen = playersTeam.value;
    const name = chosen === "other" ? playersTeamName.value.trim() : chosen;
    if (!name) {
      showStatus("error", "Choose a team, or name the one a player formed.");
      return;
    }

    send("/fake-teams/players", {
      mode: Number(modeSelect.value),
      count: Number(byId("players-count").value),
      teamName: name,
      playerPrefix: byId("players-prefix").value.trim(),
    });
  });

  onSubmit("state-form", () => {
    const teamSelect = byId("state-team-state");
    const memberSelect = byId("state-member");
    const state = teamSelect.value === "custom" ? customState(teamSelect) : Number(teamSelect.value);
    if (state === null) {
      return;
    }

    const memberState =
      memberSelect.value === "custom" ? customState(memberSelect) : Number(memberSelect.value);
    if (memberState === null) {
      return;
    }

    if (!stateTeam.value) {
      showStatus("error", "This lobby is holding no teams in memory; create one first.");
      return;
    }

    send("/fake-teams/state", {
      mode: Number(modeSelect.value),
      teamName: stateTeam.value,
      state: state,
      memberState: memberState,
    });
  });

  playersTeam.addEventListener("change", () => {
    playersTeamOther.classList.toggle("hidden", playersTeam.value !== "other");
  });

  byId("refresh").addEventListener("click", refresh);
  modeSelect.addEventListener("change", refresh);

  refresh();
})();
