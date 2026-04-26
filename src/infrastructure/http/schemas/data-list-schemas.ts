import { z } from "@hono/zod-openapi";

export const dataListRequestSchema = z.object({
  svrgid: z.coerce.number().describe("Server group ID"),
  lang: z.coerce.number().describe("Language code"),
  pid: z.coerce.number().describe("Player ID"),
});

export const dataListResponseSchema = z.string();
