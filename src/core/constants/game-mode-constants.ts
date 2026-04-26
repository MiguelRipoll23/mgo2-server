import type { CharacterStats } from "../../db/schema.ts";

// Game mode index → stats column name (matches echo's getStatsByMode)
export const MODE_COLUMN: Record<number, keyof CharacterStats> = {
  0: "stats_dm",
  1: "stats_tdm",
  2: "stats_sne",
  3: "stats_cap",
  4: "stats_base",
  5: "stats_bomb",
  6: "stats_res",
  7: "stats_race",
  8: "stats_tsne",
  9: "stats_sdm",
  10: "stats_scap",
};
