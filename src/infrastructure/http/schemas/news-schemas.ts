import { z } from "@hono/zod-openapi";

export const newsItemResponseSchema = z.object({
  id: z.number().openapi({ example: 1, description: "Unique news item identifier" }),
  important: z.boolean().openapi({ example: false, description: "Whether this item should be highlighted as important" }),
  time: z.number().openapi({ example: 1700000000, description: "Publication timestamp as a Unix epoch integer" }),
  topic: z.string().openapi({ example: "Maintenance Notice", description: "Short title of the news item (max 128 characters)" }),
  message: z.string().openapi({ example: "The server will be down for maintenance.", description: "Full body text of the news item" }),
}).openapi("NewsItemResponse");

export const newsListResponseSchema = z.array(newsItemResponseSchema);

export const newsRequestSchema = z.object({
  important: z.boolean().openapi({ example: false }),
  time: z.number().int().openapi({ example: 1700000000 }),
  topic: z.string().min(1).max(128).openapi({ example: "Maintenance Notice" }),
  message: z.string().min(1).openapi({ example: "The server will be down for maintenance." }),
});

export const newsPatchRequestSchema = newsRequestSchema.partial();

export const newsParamsSchema = z.object({
  id: z.coerce.number().int().positive(),
});

export const newsErrorSchema = z.object({
  error: z.string(),
});
