// The behaviour of the event testing tools page.
//
// The page shows two kinds of team and asks two different servers about them.
// A real team is a row, so it is read from the database by mode; an in-memory
// team lives in one lobby's process, so the only way to see it is to ask that
// lobby and wait for its answer. They are kept apart everywhere below because
// the difference decides what can be done to them: only a real team is ever
// queued and paired, and only an in-memory team can have its state set or be
// forgotten from here.
//
// Everything talks to the public endpoints, so there is no token and nothing to
// sign in with. The page is served at both /survival and /tournament and is the
// same code either way: the path only picks the lobby the page opens on.
(function () {
  "use strict";

  const teams = window.EventToolTeams;
  const maximumPlayers = teams.maximumPlayers;

  const byId = teams.byId;

  const modeSelect = byId("mode");
  const status = byId("status");
  const statusText = byId("status-text");
  const memoryList = byId("team-list");
  const realList = byId("real-team-list");
  const matchList = byId("match-list");
  const playersTeam = byId("players-team");
  const playersTeamOther = byId("players-team-other");
  const playersTeamName = byId("players-team-name");
  const stateTeam = byId("state-team");
  const memberTeam = byId("member-team");

  // The teams the last refresh returned, held so the menus and the lists on the
  // page agree with each other without asking twice.
  let realTeams = [];
  let memoryTeams = [];

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

  function fillCounts(select, chosen) {
    select.replaceChildren();
    for (let count = 1; count <= maximumPlayers; count++) {
      select.append(
        new Option(count === 1 ? "1 player" : count + " players", String(count))
      );
    }
    select.value = String(chosen);
  }

  // A refusal is reported rather than thrown away: a moderator who asked for a
  // mode with no lobby needs to know that, and it is the common case while a
  // lobby is restarting.
  async function readList(url, key) {
    const response = await fetch(url);
    if (response.ok) {
      return (await response.json())[key] || [];
    }

    showStatus("error", await readMessage(response));
    return null;
  }

  function readTeams(url) {
    return readList(url, "teams");
  }

  function renderRealList() {
    if (realTeams.length === 0) {
      teams.emptyList(realList, "No player has formed a team in this lobby.");
      return;
    }

    realList.replaceChildren();
    for (const team of realTeams) {
      realList.append(teams.row(team, "real", []));
    }
  }

  function renderMemoryList() {
    if (memoryTeams.length === 0) {
      teams.emptyList(memoryList, "This lobby is holding no teams in memory.");
      return;
    }

    memoryList.replaceChildren();
    for (const team of memoryTeams) {
      memoryList.append(
        teams.row(team, "memory", [
          teams.promoteButton(team.name, promoteTeam),
          teams.removeButton(team.name, forgetTeam),
        ])
      );
    }
  }

  // The pairings are listed apart from the teams because they answer the
  // question the team lists cannot: a pairing is only announced to its teams
  // once a room has been leased for it, so a pair that has matched and a pair
  // waiting for a host both show nothing at all in game. Without this list a
  // moderator cannot tell them apart.
  function renderMatchList(matches) {
    if (matches === null) {
      return;
    }

    if (matches.length === 0) {
      teams.emptyList(
        matchList,
        "No two teams have been paired in this lobby yet."
      );
      return;
    }

    matchList.replaceChildren();
    for (const match of matches) {
      matchList.append(teams.matchRow(match));
    }
  }

  function fillMenus() {
    teams.fillMenu(
      playersTeam,
      realTeams,
      memoryTeams,
      "Another team this lobby does not list…"
    );

    // The state form offers in-memory teams only. A real team's state is
    // written by the entry pipeline on every decision, so a value set here
    // would be overwritten rather than tested, and offering it would suggest
    // otherwise.
    teams.fillMenu(stateTeam, [], memoryTeams, null);

    // The member-state form is the other way round: it offers stored teams,
    // because a fake player that landed in a row is the one nobody else can
    // decide for. An in-memory team's roster is changed by the form above.
    teams.fillMenu(memberTeam, realTeams, [], null);
  }

  // A refresh asks all three sources. They are asked together rather than one
  // after the other because none is slow and a moderator watching the page
  // should not see one list update while the others wait.
  async function refresh() {
    showStatus("pending", "Asking the lobby and the database what they hold…");

    const mode = modeSelect.value;
    const [stored, memory, pairings] = await Promise.all([
      readTeams("/event-teams?mode=" + mode),
      readTeams("/fake-teams?mode=" + mode),
      readList("/event-matches?mode=" + mode, "matches"),
    ]);

    // A failure in either is not a failure of the other: one list can still be
    // shown, which is more useful than emptying both.
    if (stored === null && memory === null) {
      realTeams = [];
      memoryTeams = [];
      renderRealList();
      renderMemoryList();
      renderMatchList(pairings);
      fillMenus();
      return;
    }

    if (stored !== null) {
      realTeams = stored;
      renderRealList();
    }
    if (memory !== null) {
      memoryTeams = memory;
      renderMemoryList();
    }
    renderMatchList(pairings);
    fillMenus();

    const held = realTeams.length + memoryTeams.length;
    if (stored !== null && memory !== null) {
      showStatus(
        "success",
        held === 1
          ? "This lobby holds 1 team."
          : `This lobby holds ${held} teams: ${realTeams.length} stored, ${memoryTeams.length} in memory.`
      );
    }
  }

  async function forgetTeam(name) {
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

  // Writes one in-memory team out as a stored team and queues it. The team then
  // leaves the memory list and appears in the stored one, because that is what
  // it has become: a pairing is a row naming two teams, and everything
  // downstream of one re-reads both sides as rows.
  async function promoteTeam(name) {
    showStatus("pending", `Asking the lobby to write "${name}" out and queue it…`);
    try {
      const response = await fetch("/fake-teams/pairing", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ mode: Number(modeSelect.value), teamName: name }),
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
  teams.fillStates(byId("state-team-state"), teams.teamStates, "Another state byte…");
  teams.fillStates(byId("state-member"), teams.memberStates, "Another member state byte…");
  // The stored-team form does not offer "let each member follow the team":
  // that escape hatch only means anything for a team whose roster is projected
  // at read time, and a stored roster carries one state per member that the
  // client reads literally. Zero is not a member state at all.
  teams.fillStates(
    byId("member-state"),
    teams.memberStates.filter((candidate) => candidate.value !== 0),
    "Another member state byte…"
  );

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
      showStatus("error", "Choose a team, or name the one this lobby does not list.");
      return;
    }

    // A roster that is already full is refused by the server, which answers
    // with the reason. The page does not second-guess it: the count the client
    // shows is a column, and what a team can hold is the server's rule.
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

  onSubmit("member-state-form", () => {
    const memberSelect = byId("member-state");
    const memberState =
      memberSelect.value === "custom" ? customState(memberSelect) : Number(memberSelect.value);
    if (memberState === null) {
      return;
    }

    if (!memberTeam.value) {
      showStatus("error", "This lobby is holding no stored teams; form one first.");
      return;
    }

    send("/fake-teams/member-state", {
      mode: Number(modeSelect.value),
      teamName: memberTeam.value,
      memberState: memberState,
    });
  });

  // The room is created on a click rather than a submit because there is
  // nothing to fill in that a caller has to decide: it is a room for this
  // lobby's matches, and its only field is which character hosts it, which
  // zero answers without asking.
  byId("host-room").addEventListener("click", async () => {
    const character = Number(byId("host-room-character").value.trim() || "0");
    if (!Number.isInteger(character) || character < 0) {
      showStatus("error", "A character identifier is a whole number, or 0 to let the lobby pick.");
      return;
    }

    showStatus("pending", "Asking the lobby for a dedicated host room…");
    await send("/fake-teams/host-room", {
      mode: Number(modeSelect.value),
      hostCharacterIdentifier: character,
    });
  });

  byId("refresh").addEventListener("click", refresh);
  modeSelect.addEventListener("change", refresh);

  refresh();
})();
