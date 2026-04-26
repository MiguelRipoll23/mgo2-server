import { z } from "@hono/zod-openapi";

export const registerRequestSchema = z.object({
  displayName: z.string().min(3).max(32)
    .regex(/^[a-zA-Z0-9_\-]+$/, "Letters, digits, _ and - only")
    .openapi({ example: "Snake_01" }),
  password: z.string().min(6).max(128)
    .openapi({ example: "hunter2" }),
});

export const registerResponseSchema = z.object({
  id: z.number().openapi({ example: 42 }),
  displayName: z.string().openapi({ example: "Snake_01" }),
});

export const registerErrorSchema = z.object({
  error: z.string(),
});
