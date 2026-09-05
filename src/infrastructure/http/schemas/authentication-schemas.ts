import { z } from "@hono/zod-openapi";

export const loginRequestSchema = z.object({
  name: z.string().min(1).max(15).describe("Username"),
  passwd: z.string().length(32).describe("MD5 hash of password"),
  product: z.string().describe("Product ID"),
  lang: z.coerce.number().describe("Language code"),
  tz: z.coerce.number().describe("Timezone offset in minutes"),
  disk: z.coerce.number().describe("Disk flag"),
  ps3: z.coerce.number().describe("PS3 platform flag"),
  stime: z.coerce.number().describe("Server time from client"),
  seed: z.string().describe("Random seed for session"),
});

export const loginResponseSchema = z
  .string()
  .describe(
    "Format: status,userId,perks,hexToken — e.g. 0,35771,1000_1000_5000_10000_1000_3000_1000_1000_2000_1000,c1850f7a00000000. " +
      "status=0 on success, non-zero on error. The perks field is build-specific: the 1.36 " +
      "client requires ten underscore-separated integers (an empty field also parses); the 1.0 " +
      "disc client requires exactly one integer. There is no value valid for both.",
  );
