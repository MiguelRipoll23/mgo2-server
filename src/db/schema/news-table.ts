import { boolean, integer, pgTable, serial, text, varchar } from "drizzle-orm/pg-core";

export const newsTable = pgTable("news", {
  id: serial("id").primaryKey(),
  important: boolean("important").notNull().default(false),
  time: integer("time").notNull(),
  topic: varchar("topic", { length: 128 }).notNull(),
  message: text("message").notNull(),
});

export type NewsItem = typeof newsTable.$inferSelect;
export type NewNewsItem = typeof newsTable.$inferInsert;
