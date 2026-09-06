import pg from "pg";
import { drizzle } from "drizzle-orm/node-postgres";
import { migrate } from "drizzle-orm/node-postgres/migrator";

const DATABASE_URL = Deno.env.get("DATABASE_URL");

if (!DATABASE_URL) {
  console.error("[migrate] DATABASE_URL environment variable is required");
  Deno.exit(1);
}

const migrationsFolder = Deno.env.get("MIGRATIONS_FOLDER") ?? "./drizzle";

console.log(`[migrate] Connecting to database...`);
console.log(
  `[migrate] Using migrations folder: ${migrationsFolder}`,
);

const client = new pg.Client({ connectionString: DATABASE_URL });
const db = drizzle(client);

try {
  await client.connect();
  console.log("[migrate] Connected. Applying migrations...");

  await migrate(db, { migrationsFolder });

  console.log("[migrate] All migrations applied successfully.");
} catch (error) {
  console.error("\n[migrate] Migration FAILED:");

  // drizzle-orm wraps the original driver error in a DrizzleQueryError;
  // walk the cause chain so the real PostgreSQL error message is printed.
  let current: unknown = error;
  const seen = new Set<unknown>();
  while (current instanceof Error && !seen.has(current)) {
    seen.add(current);
    console.error(`[migrate] ${current.name}: ${current.message}`);
    if (current.stack) {
      console.error(current.stack);
    }
    current = current.cause;
  }
  if (current !== undefined && current !== null) {
    console.error("[migrate] Cause:", current);
  }

  Deno.exit(1);
} finally {
  await client.end().catch(() => {});
}