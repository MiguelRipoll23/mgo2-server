import { injectable, inject } from "@needle-di/core";
import { desc, eq } from "drizzle-orm";
import { DatabaseService } from "../../core/services/database-service.ts";
import { newsTable } from "../../db/schema.ts";
import type { NewsItem, NewNewsItem } from "../../db/schema.ts";
import { ServerError } from "../../core/errors/server-error.ts";


@injectable()
export class NewsService {
  constructor(private readonly dbs = inject(DatabaseService)) {}

  private get db() {
    return this.dbs.get();
  }

  async findAll(): Promise<NewsItem[]> {
    return await this.db.select().from(newsTable).orderBy(desc(newsTable.id));
  }

  async findById(id: number): Promise<NewsItem> {
    const rows = await this.db
      .select()
      .from(newsTable)
      .where(eq(newsTable.id, id))
      .limit(1);
    if (!rows[0]) {
      throw new ServerError("NOT_FOUND", "News item not found", 404);
    }
    return rows[0];
  }

  async create(data: Omit<NewNewsItem, "id">): Promise<NewsItem> {
    const [created] = await this.db.insert(newsTable).values(data).returning();
    return created;
  }

  async update(
    id: number,
    data: Partial<Omit<NewNewsItem, "id">>,
  ): Promise<NewsItem> {
    const [updated] = await this.db
      .update(newsTable)
      .set(data)
      .where(eq(newsTable.id, id))
      .returning();
    if (!updated) {
      throw new ServerError("NOT_FOUND", "News item not found", 404);
    }
    return updated;
  }

  async remove(id: number): Promise<void> {
    const deleted = await this.db
      .delete(newsTable)
      .where(eq(newsTable.id, id))
      .returning({ id: newsTable.id });
    if (deleted.length === 0) {
      throw new ServerError("NOT_FOUND", "News item not found", 404);
    }
  }
}
