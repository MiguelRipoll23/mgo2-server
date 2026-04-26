import { z } from "@hono/zod-openapi";

export const checkVerRequestSchema = z.object({
  p: z.string().describe("Comma-separated version,product,seed"),
});

export const checkVerResponseSchema = z.string();
