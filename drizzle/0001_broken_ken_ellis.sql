CREATE TABLE "character_skills" (
	"character_id" integer NOT NULL,
	"skill_id" smallint NOT NULL,
	"experience" integer DEFAULT 0 NOT NULL,
	"flag" smallint DEFAULT 0 NOT NULL,
	CONSTRAINT "character_skills_character_id_skill_id_pk" PRIMARY KEY("character_id","skill_id")
);
--> statement-breakpoint
ALTER TABLE "character_skills" ADD CONSTRAINT "character_skills_character_id_characters_id_fk" FOREIGN KEY ("character_id") REFERENCES "public"."characters"("id") ON DELETE cascade ON UPDATE no action;--> statement-breakpoint
CREATE INDEX "character_skills_skill_id_idx" ON "character_skills" USING btree ("skill_id");